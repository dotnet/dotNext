using System;
using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal interface ILocalClusterMember
    {
        ref readonly Guid Id { get; }

        long Term { get; }

        IReadOnlyDictionary<string, string> Metadata { get; }
    }
}
