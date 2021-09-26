using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using Threading;

    [ExcludeFromCodeCoverage]
    internal sealed class TestMetricsCollector : HttpMetricsCollector
    {
        internal long RequestCount, HeartbeatCount;
        internal volatile bool LeaderStateIndicator;

        public override void ReportResponseTime(TimeSpan value) => RequestCount.IncrementAndGet();

        public override void ReportHeartbeat() => HeartbeatCount.IncrementAndGet();

        public override void MovedToLeaderState() => LeaderStateIndicator = true;
    }
}