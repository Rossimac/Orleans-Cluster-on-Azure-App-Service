// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using Azure.Storage.Blobs;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Azure.Cosmos;
using Orleans.Persistence.Cosmos;
using Orleans.ShoppingCart.Silo;
using Orleans.Streams;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

if (builder.Environment.IsDevelopment())
{
    builder.UseOrleans(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            //.AddMemoryStreams("PetClaimsStreamProvider")
            .AddMemoryGrainStorage("PubSubStore")
            .AddEventHubStreams(
                "PetClaimsStreamProvider",
                configurator =>
                {
                    configurator.ConfigureEventHub(eventHubBuilder => eventHubBuilder.Configure(options =>
                    {
                        var ehConnection = builder.Configuration.GetConnectionString("event-hubs");

                        options.ConfigureEventHubConnection(ehConnection, "claims-events", "claims-events-consumer");
                    }));
                    configurator.ConfigureCachePressuring(ob => ob.Configure(pressureOptions =>
                    {
                        pressureOptions.SlowConsumingMonitorPressureWindowSize = TimeSpan.FromSeconds(1);
                        pressureOptions.AveragingCachePressureMonitorFlowControlThreshold = null;
                    }));

                    // We plug here our custom DataAdapter for Event Hub
                    configurator.UseDataAdapter(
                        (sp, n) => ActivatorUtilities.CreateInstance<CustomDataAdapter>(sp));

                    configurator.ConfigurePartitionReceiver(configure =>
                    {
                        configure.Configure(options => { options.PrefetchCount = 100; });
                    });

                    configurator.UseAzureTableCheckpointer(checkpointBuilder => checkpointBuilder.Configure(options =>
                    {
                        options.TableServiceClient =
                            new(builder.Configuration.GetConnectionString("eventHubCheckpointTable"));
                        options.TableName = "eventHubCheckpointTable";
                        //options.PersistInterval = TimeSpan.FromSeconds(10);
                    }));
                    configurator.ConfigureStreamPubSub(StreamPubSubType.ImplicitOnly);
                })
            .AddMemoryGrainStorage("grain-storage")
            .AddMemoryGrainStorage("pet-claim-grain-storage")
            // .UseDashboard(x =>
            // {
            //     x.Port = 8080;
            //     x.HostSelf = true;
            // })
            ;
    });
}
else
{
    builder.UseOrleans(siloBuilder =>
    {
#pragma warning disable ORLEANSEXP003
        siloBuilder.AddDistributedGrainDirectory();
#pragma warning restore ORLEANSEXP003

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
                        options.PersistInterval = TimeSpan.FromSeconds(10);
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

app.MapDefaultEndpoints();

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

app.MapPost("/api/create-sample-claims-file/{count:int}", async (IConfiguration config, int count) =>
{
    // --- Connect to blob ---
    var blobStorageConnectionString = config["ConnectionStrings:blobs"];

    var connectionString = blobStorageConnectionString
                           ?? throw new InvalidOperationException("Missing AzureWebJobsStorage connection string");

    var containerName = "sample-claims";
    var fileName = $"claims.csv";

    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
    await containerClient.CreateIfNotExistsAsync();

    // --- Build sample data ---
    var claims = Enumerable.Range(1, count).Select(i => new ClaimDetails
    {
        PetId = Guid.NewGuid(),
        ClaimId = $"CLM{i:D3}",
        PolicyNumber = $"POL{i * 12345:D5}",
        CustomerName = i % 2 == 0 ? "Jane Smith" : "John Doe",
        ClaimDate = DateTime.UtcNow.AddDays(-i),
        ClaimAmount = 100 + (i * 25.75m),
        Status = i % 3 == 0 ? "Approved" : "Pending",
        Description = $"Claim number {i} for veterinary expense"
    }).ToList();

    // --- Write CSV to MemoryStream using CsvHelper ---
    await using var memStream = new MemoryStream();
    await using (var writer = new StreamWriter(memStream, Encoding.UTF8, leaveOpen: true))
    await using (var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)))
    {
        csv.WriteHeader<ClaimDetails>();
        await csv.NextRecordAsync();
        await csv.WriteRecordsAsync(claims);
    }

    memStream.Position = 0;

    // --- Upload to Blob ---
    var blobClient = containerClient.GetBlobClient(fileName);
    await blobClient.UploadAsync(memStream, overwrite: true);

    return Results.Ok(new
    {
        Message = "Sample CSV created and uploaded to Blob Storage.",
        BlobUri = blobClient.Uri.ToString()
    });
});

await app.RunAsync();


public class JobTrackerPartitionKeyProvider : IPartitionKeyProvider
{
    public ValueTask<string> GetPartitionKey(string grainType, GrainId grainId)
    {
        return ValueTask.FromResult($"{grainType}.{grainId.ToString()}");
    }
}