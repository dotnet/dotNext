using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    private readonly ClusterFailureDetector? failureDetector;

    private sealed class ClusterFailureDetector
    {
        private readonly Func<TimeSpan, TMember, IFailureDetector> detectorFactory;
        private readonly ConditionalWeakTable<TMember, IFailureDetector> detectors;
        private readonly TimeSpan estimateFirstHeartbeat;

        internal ClusterFailureDetector(TimeSpan estimateFirstHeartbeat, Func<TimeSpan, TMember, IFailureDetector> factory)
        {
            Debug.Assert(estimateFirstHeartbeat > TimeSpan.Zero);
            Debug.Assert(factory is not null);

            detectorFactory = factory;
            detectors = new();
            this.estimateFirstHeartbeat = estimateFirstHeartbeat;
        }

        internal IFailureDetector GetOrCreateDetector(TMember member)
        {
            if (!detectors.TryGetValue(member, out var detector))
                detectors.Add(member, detector = detectorFactory(estimateFirstHeartbeat, member));

            return detector;
        }

        internal void Clear() => detectors.Clear();
    }

    internal Func<TimeSpan, TMember, IFailureDetector>? FailureDetectorFactory
    {
        init => failureDetector = value is not null ? new(maxLease, value) : null;
    }
}