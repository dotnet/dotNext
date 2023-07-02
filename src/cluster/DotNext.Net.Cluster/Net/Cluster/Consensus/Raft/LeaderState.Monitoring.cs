namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    private readonly Func<TimeSpan, TMember, IFailureDetector>? detectorFactory;

    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        init => detectorFactory = value;
    }
}