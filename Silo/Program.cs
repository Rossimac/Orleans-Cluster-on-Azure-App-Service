// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

using Azure.Data.Tables;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Log all environment variables
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger("Startup");
logger.LogInformation("=== Environment Variables ===");
foreach (System.Collections.DictionaryEntry envVar in Environment.GetEnvironmentVariables())
{
    logger.LogInformation("{Key} = {Value}", envVar.Key, envVar.Value);
}
logger.LogInformation("=== Configuration Values ===");
logger.LogInformation("ORLEANS_AZURE_STORAGE_CONNECTION_STRING = {Value}",
    builder.Configuration["ORLEANS_AZURE_STORAGE_CONNECTION_STRING"] ?? "NULL");
logger.LogInformation("ORLEANS_CLUSTER_ID = {Value}",
    builder.Configuration["ORLEANS_CLUSTER_ID"] ?? "NULL");
logger.LogInformation("WEBSITE_PRIVATE_IP = {Value}",
    builder.Configuration["WEBSITE_PRIVATE_IP"] ?? "NULL");
logger.LogInformation("WEBSITE_PRIVATE_PORTS = {Value}",
    builder.Configuration["WEBSITE_PRIVATE_PORTS"] ?? "NULL");
logger.LogInformation("APPLICATIONINSIGHTS_CONNECTION_STRING = {Value}",
    builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] ?? "NULL");
logger.LogInformation("=== End Configuration ===");

if (builder.Environment.IsDevelopment())
{
    builder.UseOrleans(siloBuilder =>
    {
        siloBuilder.UseLocalhostClustering().AddMemoryGrainStorage("shopping-cart");
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
            throw new Exception($"Insufficient private ports configured: WEBSITE_PRIVATE_PORTS: '{builder.Configuration["WEBSITE_PRIVATE_PORTS"]?.ToString()}' or '{env}.");
        }

        var (siloPort, gatewayPort) = (int.Parse(strPorts[0]), int.Parse(strPorts[1]));

        siloBuilder.ConfigureEndpoints(endpointAddress, siloPort, gatewayPort, listenOnAnyHostAddress: true)
        .Configure<ClusterOptions>(
            options =>
            {
                options.ClusterId = builder.Configuration["ORLEANS_CLUSTER_ID"];
                options.ServiceId = nameof(ShoppingCartService);
            })
        .UseAzureStorageClustering(options =>
        {
            options.TableServiceClient = new(builder.Configuration["ORLEANS_AZURE_STORAGE_CONNECTION_STRING"]);
            options.TableName = $"{builder.Configuration["ORLEANS_CLUSTER_ID"]}Clustering";
        })
        .AddAzureTableGrainStorage("shopping-cart",
            options =>
            {
                options.TableServiceClient = new(builder.Configuration["ORLEANS_AZURE_STORAGE_CONNECTION_STRING"]);
                options.TableName = $"{builder.Configuration["ORLEANS_CLUSTER_ID"]}Persistence";
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
    builder.Logging.AddApplicationInsights((telemetry) => telemetry.ConnectionString = appInsightsConnectionString, logger => { });
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
