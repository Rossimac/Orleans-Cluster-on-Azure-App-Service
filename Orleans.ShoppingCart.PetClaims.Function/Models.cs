// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT License.

namespace Orleans.ShoppingCart.PetClaims.Function;

    /// <summary>
    /// Input from the Scheduled Task API to the Durable Function orchestrator.
    /// </summary>
    public class ProcessRequest
    {
        public long RunId { get; set; }             // Unique ID for this scheduled run
        public string TenantId { get; set; }        // Tenant being processed
        public string FileName { get; set; }        // Original file name
        public DateTimeOffset SubmittedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Describes one chunk of the input file (e.g., a segment of lines or a chunked blob).
    /// </summary>
    public class ChunkMetadata
    {
        public long RunId { get; set; }             // Same RunId as parent
        public string TenantId { get; set; }        // Tenant identifier
        public string ChunkId { get; set; }         // Unique per run (e.g., "chunk-0001")
        public string ChunkFileName { get; set; }       // Location of the chunk blob in storage
        public int LineStart { get; set; }          // Optional - first line index (for diagnostics)
        public int LineEnd { get; set; }            // Optional - last line index
        public long LineCount { get; set; }         // Number of lines in this chunk
        public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Result of processing a single chunk.
    /// </summary>
    public class ChunkResult
    {
        public long RunId { get; set; }             // Run correlation
        public string TenantId { get; set; }        // Tenant correlation
        public string ChunkId { get; set; }         // Same as input metadata
        public bool Success { get; set; }           // Did the chunk process fully?
        public long LinesProcessed { get; set; }    // Number of lines successfully processed
        public long LinesFailed { get; set; }       // Number of lines that failed
        public string ErrorMessage { get; set; }    // Optional - if failed
        public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletedAtUtc { get; set; }
    }

    /// <summary>
    /// Overall aggregation result at the end of the orchestrator.
    /// </summary>
    public class RunSummary
    {
        public long RunId { get; set; }
        public string TenantId { get; set; }
        public string FileName { get; set; }
        public DateTimeOffset StartedAtUtc { get; set; }
        public DateTimeOffset CompletedAtUtc { get; set; }
        public int TotalChunks { get; set; }
        public long TotalLinesProcessed { get; set; }
        public long TotalLinesFailed { get; set; }
        public int SuccessfulChunks { get; set; }
        public int FailedChunks { get; set; }
        public bool Success => FailedChunks == 0;
    }