using System.Text.Json.Nodes;
using Azure.Provisioning.Storage;

var builder = DistributedApplication.CreateBuilder(args);

//var explorer = builder.AddAzureEventHubsLiveExplorer("event-hub-viewer");

var eventHubs = builder.AddAzureEventHubs("event-hubs")
    .RunAsEmulator(emulator =>
    {
        //emulator.WithLifetime(ContainerLifetime.Persistent);
        //emulator.WithDataVolume();
        emulator.WithConfiguration(
            (JsonNode configuration) =>
            {
                var userConfig = configuration["UserConfig"];
                var ns = userConfig["NamespaceConfig"][0];
                var firstEntity = ns["Entities"][0];

                firstEntity["PartitionCount"] = 32;
            });
    });

var messages = eventHubs.AddHub("claims-events");
var consumerGroup = messages.AddConsumerGroup("claims-events-consumer");

//explorer.WithReference(consumerGroup);

var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(azurite =>
    {
        // azurite.WithLifetime(ContainerLifetime.Persistent);
        // azurite.WithDataVolume();
    });

var blobs = storage
    .AddBlobs("blobs");

var eventHubCheckpointTable = storage
    .AddTables("eventHubCheckpointTable");

builder.AddProject<Projects.Orleans_ShoppingCart_Silo>("orleans-shoppingcart-silo")
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(eventHubs)
    .WaitFor(eventHubs)
    .WithReference(eventHubCheckpointTable)
    .WaitFor(eventHubCheckpointTable);

builder.AddAzureFunctionsProject<Projects.Orleans_ShoppingCart_PetClaims_Function>("orleans-petclaims-function")
    .WithHostStorage(storage)
    .WithRoleAssignments(storage, StorageBuiltInRole.StorageBlobDataOwner)
    .WithReference(blobs)
    .WaitFor(blobs)
    .WithReference(eventHubs)
    .WaitFor(eventHubs);

builder.Build().Run();