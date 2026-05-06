namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory { private get; init; }
}