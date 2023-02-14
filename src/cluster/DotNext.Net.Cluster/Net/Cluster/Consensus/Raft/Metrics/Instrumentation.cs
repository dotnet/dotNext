using System.Diagnostics.Metrics;

namespace DotNext.Net.Cluster.Consensus.Raft.Metrics;

internal static class Instrumentation
{
    internal static readonly Meter ServerSide = new("DotNext.Net.Cluster.Consensus.Raft.Server");

    internal static readonly Meter ClientSide = new("DotNext.Net.Cluster.Consensus.Raft.Client");
}