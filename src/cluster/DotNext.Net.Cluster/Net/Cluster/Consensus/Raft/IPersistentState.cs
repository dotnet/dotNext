using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Replication;

    /// <summary>
    /// Represents persistent state of local cluster member
    /// required by Raft consensus protocol.
    /// </summary>
    public interface IPersistentState : IAuditTrail<LogEntryId>
    {
        /// <summary>
        /// Determ
        /// </summary>
        /// <param name="member"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> IsVotedForAsync(IRaftClusterMember member, CancellationToken token);

        /// <summary>
        /// Reads Term value associated with the local member
        /// from the persistent storage.
        /// </summary>
        /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
        /// <returns>The term restored from persistent storage.</returns>
        Task<long> RestoreTermAsync(CancellationToken token);

        Task SaveTermAsync(long term, CancellationToken token);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="member">The member which identifier should be stored inside of persistence storage. May be <see langword="null"/>.</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task SaveLastVoteAsync(IRaftClusterMember member, CancellationToken token);
    }
}
