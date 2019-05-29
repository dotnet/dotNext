using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    internal interface ILocalMember
    {
        string Name { get; }
        Guid Id { get; }

        long Term { get; }
    }
}
