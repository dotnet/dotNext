using System.Runtime.CompilerServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Diagnostics;

internal partial class LeaderState<TMember>
{
    private readonly ClusterFailureDetector? failureDetector;

    private sealed class ClusterFailureDetector
    {
        private readonly Func<TMember, IFailureDetector> detectorFactory;
        private readonly ConditionalWeakTable<TMember, IFailureDetector> detectors;

        internal ClusterFailureDetector(Func<TMember, IFailureDetector> factory)
        {
            Debug.Assert(factory is not null);

            detectorFactory = factory;
            detectors = new();
        }

        internal void ReportHeartbeat(TMember member)
        {
            if (!detectors.TryGetValue(member, out var detector))
                detectors.Add(member, detector = detectorFactory(member));

            detector.ReportHeartbeat();
        }

        internal bool IsAlive(TMember member) => detectors.TryGetValue(member, out var detector) ? detector.IsHealthy : true;

        internal void Clear() => detectors.Clear();
    }

    internal Func<TMember, IFailureDetector>? FailureDetectorFactory
    {
        init => failureDetector = value is not null ? new(value) : null;
    }
}