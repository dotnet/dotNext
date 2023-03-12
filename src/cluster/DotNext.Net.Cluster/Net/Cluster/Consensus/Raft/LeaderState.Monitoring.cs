using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    private readonly ConditionalWeakTable<TMember, IFailureDetector>.CreateValueCallback? detectorFactory;
    private readonly ConditionalWeakTable<TMember, IFailureDetector>? detectors;

    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        init
        {
            if (value is null)
            {
                detectorFactory = null;
                detectors = null;
            }
            else
            {
                detectors = new();
                detectorFactory = member => value(maxLease, member);
            }
        }
    }
}