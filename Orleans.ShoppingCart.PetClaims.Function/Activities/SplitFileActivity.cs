using System.Globalization;
using Azure.Storage.Blobs;
using CsvHelper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Orleans.ShoppingCart.PetClaims.Function.Activities;

public class SplitFileActivity
{
    private readonly IConfiguration _configuration;

    public SplitFileActivity(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function(nameof(SplitFileActivity))]
    public async Task<List<ChunkMetadata>> SplitFile([ActivityTrigger] ProcessRequest input, string instanceId,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger(nameof(SplitFileActivity));

        using (logger.BeginScope(new Dictionary<string, object> { ["OrchestrationInstanceId"] = instanceId }))
        {
            logger.LogInformation("Saying hello");
        }

        var runId = input.RunId;
        var tenantId = input.TenantId;
        const int chunkSize = 10_000;

        var blobServiceClient = new BlobServiceClient(_configuration["blobs"]);
        var containerClient = blobServiceClient.GetBlobContainerClient("sample-claims");

        var chunks = new List<ChunkMetadata>();
        var chunkIndex = 0;
        var lineCount = 0;

        logger.LogInformation("Splitting blob into chunks of {ChunkSize} lines", chunkSize);

        var blobClient = containerClient.GetBlobClient(input.FileName);

        await using var blobStream = await blobClient.OpenReadAsync();
        using var reader = new StreamReader(blobStream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        await csv.ReadAsync();
        csv.ReadHeader();
        var headerRow = csv.HeaderRecord;
        var header = string.Join(",", headerRow);

        var chunkRows = new List<string>();

        while (await csv.ReadAsync())
        {
            var row = string.Join(",", csv.Parser.Record);
            chunkRows.Add(row);
            lineCount++;

            if (chunkRows.Count >= chunkSize)
            {
                chunkIndex++;
                var chunkFileName = $"{tenantId}/{runId}/chunk-{chunkIndex:D4}.csv";
                await WriteChunkToBlobAsync(containerClient, chunkFileName, header, chunkRows);

                chunks.Add(new ChunkMetadata
                {
                    RunId = runId,
                    TenantId = tenantId,
                    ChunkId = $"chunk-{chunkIndex:D4}",
                    LineStart = lineCount - chunkRows.Count,
                    LineEnd = lineCount - 1,
                    LineCount = chunkRows.Count,
                    ChunkFileName = chunkFileName
                });

                chunkRows.Clear();
            }
        }

        // Flush remaining rows
        if (chunkRows.Count > 0)
        {
            chunkIndex++;
            var chunkFileName = $"{tenantId}/{runId}/chunk-{chunkIndex:D4}.csv";
            await WriteChunkToBlobAsync(containerClient, chunkFileName, header, chunkRows);

            chunks.Add(new ChunkMetadata
            {
                RunId = runId,
                TenantId = tenantId,
                ChunkId = $"chunk-{chunkIndex:D4}",
                LineStart = lineCount - chunkRows.Count,
                LineEnd = lineCount - 1,
                LineCount = chunkRows.Count,
                ChunkFileName = chunkFileName
            });
        }

        logger.LogInformation("Split complete: {TotalChunks} chunks created with total {TotalLines} lines",
            chunks.Count, lineCount);

        return chunks;
    }

    private static async Task WriteChunkToBlobAsync(
        BlobContainerClient containerClient,
        string chunkFileName,
        string header,
        List<string> lines)
    {
        var chunkClient = containerClient.GetBlobClient(chunkFileName);
        await using var memStream = new MemoryStream();
        await using (var writer = new StreamWriter(memStream, leaveOpen: true))
        {
            await writer.WriteLineAsync(header);
            foreach (var line in lines)
            {
                await writer.WriteLineAsync(line);
            }
        }

        memStream.Position = 0;
        await chunkClient.UploadAsync(memStream, overwrite: true);
    }
}