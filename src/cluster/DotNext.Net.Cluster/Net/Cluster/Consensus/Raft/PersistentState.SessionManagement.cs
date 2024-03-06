using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using AtomicBoolean = Threading.Atomic.Boolean;
using IndexPool = Collections.Concurrent.IndexPool;

public partial class PersistentState
{
    /// <summary>
    /// Represents session pool that is responsible
    /// for returning a unique value in range [0..N) for each requester.
    /// </summary>
    private protected abstract class SessionIdPool
    {
        internal abstract int Take();

        internal abstract void Return(int sessionId);
    }

    private sealed class FastSessionIdPool : SessionIdPool
    {
        private IndexPool indicies = new();

        internal static int MaxReadersCount => IndexPool.Capacity;

        internal override int Take() => indicies.Take();

        internal override void Return(int sessionId) => indicies.Return(sessionId);
    }

    private sealed class SlowSessionIdPool : SessionIdPool
    {
        // index in the array represents session identifier
        // if true then session identifier is available;
        // otherwise, false.
        private readonly AtomicBoolean[] tokens;

        internal SlowSessionIdPool(int poolSize)
        {
            tokens = new AtomicBoolean[poolSize];
            Array.Fill(tokens, new AtomicBoolean(true));
        }

        internal override int Take()
        {
            // fast path attempt to obtain session ID is o(1)
            var sessionId = (uint)Environment.CurrentManagedThreadId % (uint)tokens.Length;
            ref var first = ref MemoryMarshal.GetArrayDataReference(tokens);
            if (Unsafe.Add(ref first, sessionId).TrueToFalse())
                goto exit;

            // slow path - enumerate over all slots in search of available ID
            repeat_search:
            for (sessionId = 0U; sessionId < (uint)tokens.Length; sessionId++)
            {
                if (Unsafe.Add(ref first, sessionId).TrueToFalse())
                    goto exit;
            }

            goto repeat_search;

            exit:
            return (int)sessionId;
        }

        internal override void Return(int sessionId)
            => tokens[sessionId].Value = true;
    }

    // concurrent read sessions management
    private protected readonly SessionIdPool sessionManager;
}