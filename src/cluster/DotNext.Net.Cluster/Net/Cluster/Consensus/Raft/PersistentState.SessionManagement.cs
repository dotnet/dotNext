using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using AtomicBoolean = Threading.AtomicBoolean;

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
        internal const int MaxReadersCount = 63;

        // all bits are set to 1
        // if bit at position N is 1 then N is available session identifier;
        // otherwise, session identifier N is acquired by another thread
        private ulong control = ulong.MaxValue;

        internal override int Take()
        {
            int sessionId;
            ulong current, newValue;
            do
            {
                current = control;
                sessionId = BitOperations.TrailingZeroCount(current);
                newValue = current ^ (1UL << sessionId);
            }
            while (Interlocked.CompareExchange(ref control, newValue, current) != current);

            return sessionId;
        }

        internal override void Return(int sessionId)
        {
            ulong current, newValue;
            do
            {
                current = control;
                newValue = current | (1UL << sessionId);
            }
            while (Interlocked.CompareExchange(ref control, newValue, current) != current);
        }
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
            var sessionId = (Environment.CurrentManagedThreadId & int.MaxValue) % tokens.Length;
            ref var first = ref MemoryMarshal.GetArrayDataReference(tokens);
            if (Unsafe.Add(ref first, sessionId).TrueToFalse())
                goto exit;

            // slow path - enumerate over all slots in search of available ID
            repeat_search:
            for (sessionId = 0; sessionId < tokens.Length; sessionId++)
            {
                if (Unsafe.Add(ref first, sessionId).TrueToFalse())
                    goto exit;
            }

            goto repeat_search;

            exit:
            return sessionId;
        }

        internal override void Return(int sessionId)
            => tokens[sessionId].Value = true;
    }

    // concurrent read sessions management
    private protected readonly SessionIdPool sessionManager;
}