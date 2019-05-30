namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Represents context of Raft request message.
    /// </summary>
    public readonly struct RequestContext
    {
        private readonly ClusterMemberStatusChanged callback;

        internal RequestContext(ClusterMemberStatusChanged callback)
        {
            this.callback = callback;
        }

        public void MemberStatusChanged(IRaftClusterMember member, ClusterMemberStatus previousStatus, ClusterMemberStatus newStatus)
            => callback?.Invoke(member, previousStatus, newStatus);
    }
}