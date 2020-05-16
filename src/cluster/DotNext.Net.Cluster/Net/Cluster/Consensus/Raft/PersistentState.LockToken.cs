using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Threading.Timeout;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using static Threading.AtomicInt64;

    public partial class PersistentState
    {
        private long lockVersion;

        /// <summary>
        /// Represents the token describing acquired write lock.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct WriteLockToken : IDisposable
        {
            private readonly long version;
            private readonly PersistentState state;

            internal WriteLockToken(PersistentState state)
            {
                this.state = state;
                version = state.lockVersion.VolatileRead();
            }

            internal bool IsValid(PersistentState state)
                => ReferenceEquals(this.state, state) && state.lockVersion.VolatileRead() == version;

            /// <summary>
            /// Releases write lock.
            /// </summary>
            public void Dispose()
            {
                if (state.lockVersion.CompareAndSet(version, version + 1L))
                    state.syncRoot.Release();
                else
                    Debug.Fail(ExceptionMessages.InvalidLockToken);
            }
        }

        /// <summary>
        /// Determines whether the specified lock token is valid.
        /// </summary>
        /// <param name="token">The token of acquired lock to verify.</param>
        /// <returns><see langword="true"/> if <paramref name="token"/> is valid; otherwise, <see langword="false"/>.</returns>
        public bool IsValidToken(in WriteLockToken token)
            => token.IsValid(this);

        /// <summary>
        /// Acquires write lock so the caller has exclusive rights to write the entries.
        /// </summary>
        /// <param name="timeout">Lock acquisition timeout.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The token representing acquired write lock.</returns>
        /// <exception cref="TimeoutException">The lock has not been acquired in the specified time window.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public async Task<WriteLockToken> AcquireWriteLockAsync(TimeSpan timeout, CancellationToken token = default)
        {
            await syncRoot.AcquireAsync(true, timeout, token).ConfigureAwait(false);
            return new WriteLockToken(this);
        }

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