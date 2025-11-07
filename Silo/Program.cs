// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Azure.Cosmos;
using Orleans.Persistence.Cosmos;
using Orleans.Streams;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.UseOrleans(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .AddMemoryStreams("PetClaimsStreamProvider")
            .AddMemoryGrainStorage("PubSubStore")
            .AddMemoryGrainStorage("grain-storage")
            .AddMemoryGrainStorage("pet-claim-grain-storage");
    });
}
else
{
    builder.UseOrleans(siloBuilder =>
    {
#pragma warning disable ORLEANSEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        siloBuilder.AddDistributedGrainDirectory();
#pragma warning restore ORLEANSEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var endpointAddress = IPAddress.Parse(builder.Configuration["WEBSITE_PRIVATE_IP"]!);
        var strPorts = builder.Configuration["WEBSITE_PRIVATE_PORTS"]!.Split(',');
        if (strPorts.Length < 2)
        {
            var env = Environment.GetEnvironmentVariable("WEBSITE_PRIVATE_PORTS");
            throw new Exception(
                $"Insufficient private ports configured: WEBSITE_PRIVATE_PORTS: '{builder.Configuration["WEBSITE_PRIVATE_PORTS"]?.ToString()}' or '{env}.");
        }

        var (siloPort, gatewayPort) = (int.Parse(strPorts[0]), int.Parse(strPorts[1]));

        var endpoint = builder.Configuration["COSMOS_ENDPOINT"];
        var key = builder.Configuration["COSMOS_PRIMARY_KEY"];
        var db = builder.Configuration["COSMOS_DATABASE_NAME"];

        var cosmosStoreOptions = new Action<CosmosGrainStorageOptions>(opt =>
        {
            opt.DatabaseName = db;
            opt.ContainerName = "general";
            opt.ContainerThroughputProperties = ThroughputProperties.CreateAutoscaleThroughput(1000);
            opt.ConfigureCosmosClient($"AccountEndpoint={endpoint};AccountKey={key};");
        });

        siloBuilder.ConfigureEndpoints(endpointAddress, siloPort, gatewayPort, listenOnAnyHostAddress: true)
            .Configure<ClusterOptions>(options =>
            {
                options.ClusterId = builder.Configuration["ORLEANS_CLUSTER_ID"];
                options.ServiceId = nameof(ShoppingCartService);
            })
            .UseAzureStorageClustering(options =>
            {
                options.TableServiceClient = new(builder.Configuration["ORLEANS_AZURE_STORAGE_CONNECTION_STRING"]);
                options.TableName = $"{builder.Configuration["ORLEANS_CLUSTER_ID"]}Clustering";
            })
            .AddAzureTableGrainStorage("grain-storage",
                options =>
                {
                    options.TableServiceClient = new(builder.Configuration["ORLEANS_AZURE_STORAGE_CONNECTION_STRING"]);
                    options.TableName = $"{builder.Configuration["ORLEANS_CLUSTER_ID"]}Persistence";
                })
            .AddCosmosGrainStorage(
                name: "grain-storage",
                configureOptions: cosmosStoreOptions)
            .AddCosmosGrainStorage(
                name: "pet-claims-grain-storage",
                configureOptions: options =>
                {
                    cosmosStoreOptions(options);
                    options.ContainerName = "pet-claims";
                },
                typeof(JobTrackerPartitionKeyProvider))
            .AddEventHubStreams(
                "PetClaimsStreamProvider",
                configurator =>
                {
                    configurator.ConfigureEventHub(eventHubBuilder => eventHubBuilder.Configure(options =>
                    {
                        var ehConnection = builder.Configuration["EVENTHUB_CONNECTION_STRING"];
                        var ehName = builder.Configuration["EVENTHUB_NAME"];
                        var ehConsumer = builder.Configuration["EVENTHUB_CONSUMER_GROUP"] ?? "$Default";

                        options.ConfigureEventHubConnection(ehConnection, ehName, ehConsumer);
                    }));

                    configurator.ConfigurePartitionReceiver(configure =>
                    {
                        configure.Configure(options => { options.PrefetchCount = 100; });
                    });

                    configurator.UseAzureTableCheckpointer(checkpointBuilder => checkpointBuilder.Configure(options =>
                    {
                        options.TableServiceClient = new(builder.Configuration["CHECKPOINT_STORAGE_CONNECTION_STRING"]);
                        options.TableName = $"{builder.Configuration["ORLEANS_CLUSTER_ID"]}EventHubCheckpoints";
                    }));
                    configurator.ConfigureStreamPubSub(StreamPubSubType.ImplicitOnly);
                });
    });
}

var services = builder.Services;
services.AddMudServices();
services.AddRazorPages();
services.AddServerSideBlazor();
services.AddHttpContextAccessor();
services.AddSingleton<ShoppingCartService>();
services.AddSingleton<InventoryService>();
services.AddSingleton<ProductService>();
services.AddScoped<ComponentStateChangedObserver>();
services.AddSingleton<ToastService>();
services.AddLocalStorageServices();
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(appInsightsConnectionString))
{
    services.AddApplicationInsights("Silo");
    builder.Logging.AddApplicationInsights((telemetry) => telemetry.ConnectionString = appInsightsConnectionString,
        logger => { });
}

builder.Services.AddHostedService<ProductStoreSeeder>();

var app = builder.Build();

if (builder.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
await app.RunAsync();


public class JobTrackerPartitionKeyProvider : IPartitionKeyProvider
{
    public ValueTask<string> GetPartitionKey(string grainType, GrainId grainId)
    {
        return ValueTask.FromResult($"{grainType}.{grainId.ToString()}");
    }
}