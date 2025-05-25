using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    private static readonly Counter<long> CommitRateMeter, FlushRateMeter, AppendRateMeter, ApplyRateMeter;
    private static readonly Histogram<long> BytesWrittenMeter, BytesDeletedMeter;
    private static readonly Histogram<double> FlushDurationMeter, ApplyDurationMeter;
    private readonly TagList measurementTags;
    
    static WriteAheadLog()
    {
        var meter = new Meter("DotNext.IO.WriteAheadLog");
        CommitRateMeter = meter.CreateCounter<long>("entries-commit-count", description: "Number of Log Entries Committed");
        FlushRateMeter = meter.CreateCounter<long>("entries-flush-count", description: "Number of Log Entries Flushed to Disk");
        AppendRateMeter = meter.CreateCounter<long>("entries-append-count", description: "Number of Log Entries Added");
        ApplyRateMeter = meter.CreateCounter<long>("entries-apply-count", description: "Number of Log Entries Applied to the State Machine");
        
        BytesWrittenMeter = meter.CreateHistogram<long>("entries-append-bytes", unit: "bytes", description: "Number of Written Bytes");
        BytesDeletedMeter = meter.CreateHistogram<long>("entries-deleted-bytes", unit: "bytes", description: "Number of Bytes Removed from Disk");
        
        FlushDurationMeter = meter.CreateHistogram<double>("entries-flush-duration", unit: "ms",
            description: "Amount of Time Required to Write Committed Log Entries to Disk");
        ApplyDurationMeter = meter.CreateHistogram<double>("entries-apply-duration", unit: "ms",
            description: "Amount of Time Required to Apply Committed Log Entries to State Machine");
    }
}