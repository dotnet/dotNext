using System.Collections.Concurrent;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

partial class WriteAheadLog
{
    private readonly ConcurrentDictionary<long, object?> context;
}