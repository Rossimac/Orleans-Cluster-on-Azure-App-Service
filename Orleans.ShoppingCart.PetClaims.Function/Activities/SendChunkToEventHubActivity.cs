using System.Globalization;
using System.Text.Json;
using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using CsvHelper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans.ShoppingCart.Abstractions;

namespace Orleans.ShoppingCart.PetClaims.Function.Activities;

public class SendChunkToEventHubActivity
{
    private readonly IConfiguration _configuration;
    private const string EventHubConnectionSetting = "event-hubs";
    private const string EventHubName = "claims-events";

    public SendChunkToEventHubActivity(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function(nameof(SendChunkToEventHubActivity))]
    public async Task SendChunkToEventHubAsync([ActivityTrigger] ChunkMetadata chunk, string instanceId,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(SendChunkToEventHubActivity));

        var blobServiceClient = new BlobServiceClient(_configuration["blobs"]);
        var containerClient = blobServiceClient.GetBlobContainerClient("sample-claims");

        var blobClient = containerClient.GetBlobClient(chunk.ChunkFileName);

        var activeBatches = new Dictionary<string, EventDataBatch>();

        await using var blobStream = await blobClient.OpenReadAsync();
        using var reader = new StreamReader(blobStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await using var producerClient = new EventHubProducerClient(
            Environment.GetEnvironmentVariable(EventHubConnectionSetting),
            EventHubName);

        long linesProcessed = 0;

        try
        {
            await foreach (var record in ReadCsvRecordsAsync(csv))
            {
                var petId = record.PetId;

                var key = GetPartitionKey(petId);

                if (!activeBatches.TryGetValue(key, out var batch))
                {
                    batch = await producerClient.CreateBatchAsync(new CreateBatchOptions
                    {
                        PartitionKey = key
                    });

                    activeBatches[key] = batch;
                }

                var evt = new ClaimEvent
                {
                    TenantId = chunk.TenantId,
                    RunId = chunk.RunId,
                    Line = JsonSerializer.Serialize(record)
                };

                var eventData = new EventData(JsonSerializer.SerializeToUtf8Bytes(evt));
                eventData.Properties["StreamNamespace"] = "PetClaim";

                if (!batch.TryAdd(eventData))
                {
                    // batch full => send
                    await producerClient.SendAsync(batch);

                    // new batch for same partition key
                    batch = await producerClient.CreateBatchAsync(new CreateBatchOptions
                    {
                        PartitionKey = key
                    });
                    activeBatches[key] = batch;

                    batch.TryAdd(eventData);
                }

                linesProcessed++;
            }

            // Flush any remaining batches
            foreach (var batch in activeBatches.Values.Where(batch => batch.Count > 0))
            {
                await producerClient.SendAsync(batch);
            }

            using (logger.BeginScope(new Dictionary<string, object> { ["OrchestrationInstanceId"] = instanceId }))
            {
                logger.LogInformation("Chunk {ChunkId} processed: {LinesProcessed} lines sent to EventHub",
                    chunk.ChunkId, linesProcessed);
            }
        }
        finally
        {
            //eventBatch.Dispose();
        }
    }

    private static string GetPartitionKey(Guid petId)
    {
        // 32 event hub partitions
        const int partitionCount = 32;

        // hash the GUID
        var bytes = petId.ToByteArray();
        var hash = BitConverter.ToInt32(bytes, 0);

        var partitionIndex = Math.Abs(hash % partitionCount);
        return partitionIndex.ToString();
    }

    private static async IAsyncEnumerable<ClaimDetails> ReadCsvRecordsAsync(CsvReader csv)
    {
        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            // CsvHelper automatically maps header columns to properties by name
            yield return csv.GetRecord<ClaimDetails>();
        }
    }
}