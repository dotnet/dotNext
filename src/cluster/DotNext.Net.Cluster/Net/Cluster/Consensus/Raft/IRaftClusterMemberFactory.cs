using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public interface IRaftClusterMemberFactory : IClusterMemberConfiguration
    {
        IReadOnlyCollection<IRaftClusterMember> CreateMembers();
    }
}
