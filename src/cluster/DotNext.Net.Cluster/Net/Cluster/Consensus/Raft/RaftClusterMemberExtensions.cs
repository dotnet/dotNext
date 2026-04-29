namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;

internal static class RaftClusterMemberExtensions
{
    public static void Initialize(this ref IRaftClusterMember.ReplicationState state, IAuditTrail auditTrail)
    {
        state.NextIndex = auditTrail.LastEntryIndex + 1;
        state.IsAvailable = true;
    }
}