using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans.ShoppingCart.PetClaims.Function.Activities;

namespace Orleans.ShoppingCart.PetClaims.Function;

public class ClaimsProcessingOrchestration
{
    private readonly ILogger<ClaimsProcessingOrchestration> _logger;
    private readonly IConfiguration _configuration;

    public ClaimsProcessingOrchestration(ILogger<ClaimsProcessingOrchestration> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    [Function("ClaimsProcessingOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]
        HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        var logger = executionContext.GetLogger("ClaimsProcessingOrchestration_HttpStart");

        var request = new ProcessRequest
        {
            RunId = (long)DateTime.Now.TimeOfDay.TotalSeconds,
            TenantId = Guid.NewGuid().ToString(),
            FileName = "claims.csv",
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        var instanceId =
            await client.ScheduleNewOrchestrationInstanceAsync(nameof(ClaimsProcessingOrchestration), request);

        logger.LogInformation("Started orchestration with ID = '{instanceId}'", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function(nameof(ClaimsProcessingOrchestration))]
    public static async Task RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(ClaimsProcessingOrchestration));

        var request = ctx.GetInput<ProcessRequest>();

        var chunks = await ctx.CallActivityAsync<List<ChunkMetadata>>(nameof(SplitFileActivity), request);

        var chunkTasks = new List<Task<ChunkResult>>();
        foreach (var chunk in chunks)
        {
            // Start a sub-orchestration per chunk and pass chunk metadata
            chunkTasks.Add(ctx.CallSubOrchestratorAsync<ChunkResult>("SubOrchestrator_ProcessChunk", chunk));
        }

        var results = await Task.WhenAll(chunkTasks);

        var summary = new RunSummary
        {
            RunId = request.RunId,
            TenantId = request.TenantId,
            FileName = request.FileName,
            StartedAtUtc = request.SubmittedAtUtc,
            CompletedAtUtc = ctx.CurrentUtcDateTime,
            TotalChunks = results.Length,
            SuccessfulChunks = results.Count(r => r.Success),
            FailedChunks = results.Count(r => !r.Success),
            TotalLinesProcessed = results.Sum(r => r.LinesProcessed),
            TotalLinesFailed = results.Sum(r => r.LinesFailed)
        };

        //await ctx.CallActivityAsync("Activity_WriteScheduledTaskHistory", summary);

        ctx.SetCustomStatus(new { State = "Completed", SuccessCount = results.Count(r => r.Success) });
    }

    [Function("SubOrchestrator_ProcessChunk")]
    public static async Task<ChunkResult> SubClaimsOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext ctx)
    {
        var logger = ctx.CreateReplaySafeLogger(nameof(SubClaimsOrchestrator));

        var chunk = ctx.GetInput<ChunkMetadata>();
        var result = new ChunkResult
        {
            RunId = chunk.RunId,
            TenantId = chunk.TenantId,
            ChunkId = chunk.ChunkId,
            StartedAtUtc = ctx.CurrentUtcDateTime
        };

        try
        {
            // Send chunk lines to EventHub
            await ctx.CallActivityAsync(nameof(SendChunkToEventHubActivity), chunk);

            result.Success = true;
            result.LinesProcessed = chunk.LineCount;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.LinesFailed = chunk.LineCount;
        }

        result.CompletedAtUtc = ctx.CurrentUtcDateTime;
        return result;
    }



}