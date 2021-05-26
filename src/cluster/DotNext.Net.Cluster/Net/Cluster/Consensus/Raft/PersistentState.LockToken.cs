using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    public partial class PersistentState
    {
        /// <summary>
        /// Represents the token describing acquired write lock.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct WriteLockToken : IDisposable
        {
            private readonly long version;
            private readonly IWriteLock state;

            internal WriteLockToken(IWriteLock state)
            {
                this.state = state;
                version = state.Version;
            }

            internal bool IsValid(IWriteLock state)
                => ReferenceEquals(this.state, state) && state.Version == version;

            /// <summary>
            /// Releases write lock.
            /// </summary>
            public void Dispose() => state.Release(version);
        }

        /// <summary>
        /// Determines whether the specified lock token is valid.
        /// </summary>
        /// <param name="token">The token of acquired lock to verify.</param>
        /// <returns><see langword="true"/> if <paramref name="token"/> is valid; otherwise, <see langword="false"/>.</returns>
        public bool Validate(in WriteLockToken token)
            => token.IsValid(syncRoot);

        /// <summary>
        /// Acquires write lock so the caller has exclusive rights to write the entries except snapshot installation.
        /// </summary>
        /// <param name="timeout">Lock acquisition timeout.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The token representing acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock has not been acquired in the specified time window.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async Task<WriteLockToken> AcquireWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
            => await syncRoot.AcquireAsync(LockType.WriteLock, timeout, token).ConfigureAwait(false) ? new WriteLockToken(syncRoot) : throw new TimeoutException();

        /// <summary>
        /// Acquires write lock so the caller has exclusive rights to write the entries.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The token representing acquired write lock.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<WriteLockToken> AcquireWriteLockAsync(CancellationToken token)
            => AcquireWriteLockAsync(InfiniteTimeSpan, token);
    }
}