using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using AtomicBoolean = Threading.AtomicBoolean;

    public partial class PersistentState
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct DataAccessSession : IDisposable
        {
            internal readonly int SessionId;
            private readonly MemoryOwner<byte> owner;

            // read session ctor
            internal DataAccessSession(int sessionId, MemoryAllocator<byte>? bufferPool, int bufferSize)
            {
                SessionId = sessionId;
                owner = bufferPool.Invoke(bufferSize, false);
            }

            internal readonly Memory<byte> Buffer => owner.Memory;

            public void Dispose() => owner.Dispose();
        }

        /// <summary>
        /// Represents session pool that is responsible
        /// for returning a unique value in range [0..N) for each requester.
        /// </summary>
        private abstract class SessionIdPool
        {
            internal abstract int Take();

            internal abstract void Return(int sessionId);
        }

        // fast session pool supports no more than 31 readers
        // and represents concurrent power set
        private sealed class FastSessionIdPool : SessionIdPool
        {
            internal const int MaxReadersCount = 31;

#if NETSTANDARD2_1
            private static readonly byte[] TrailingZeroCountDeBruijn =
            {
                00, 01, 28, 02, 29, 14, 24, 03,
                30, 22, 20, 15, 25, 17, 04, 08,
                31, 27, 13, 23, 21, 19, 16, 07,
                26, 12, 18, 06, 11, 05, 10, 09
            };
#endif

            // all bits are set to 1
            // if bit at position N is 1 then N is available session identifier;
            // otherwise, session identifier N is acquired by another thread
            private volatile int control = -1;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static int TrailingZeroCount(int value)
            {
#if NETSTANDARD2_1
                ref var first = ref TrailingZeroCountDeBruijn[0];
                return Unsafe.AddByteOffset(ref first, (IntPtr)(int)(((value & (uint)-(int)value) * 0x077CB531U) >> 27));
#else
                return BitOperations.TrailingZeroCount(value);
#endif
            }

            internal override int Take()
            {
                int current, newValue, sessionId;
                do
                {
                    current = control;
                    sessionId = TrailingZeroCount(current);
                    newValue = current ^ (1 << sessionId);
                }
                while (Interlocked.CompareExchange(ref control, newValue, current) != current);

                return sessionId;
            }

            internal override void Return(int sessionId)
            {
                int current, newValue;
                do
                {
                    current = control;
                    newValue = current | (1 << sessionId);
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
                // fast path attempt to obtain session ID in o(1)
                var sessionId = (Thread.CurrentThread.ManagedThreadId & int.MaxValue) % tokens.Length;
                if (tokens[sessionId].TrueToFalse())
                    goto exit;

                // slow path - enumerate over all slots in search of available ID
                repeat_search:
                for (sessionId = 0; sessionId < tokens.Length; sessionId++)
                {
                    if (tokens[sessionId].TrueToFalse())
                        goto exit;
                }

                goto repeat_search;

                exit:
                return sessionId;
            }

            internal override void Return(int sessionId)
                => tokens[sessionId].Value = true;
        }

        /*
         * This class helps to organize thread-safe concurrent access to the multiple streams
         * used for reading log entries. Such approach allows to use one-writer multiple-reader scenario
         * which dramatically improves the performance
         */
        [StructLayout(LayoutKind.Auto)]
        private readonly struct DataAccessSessionManager : IDisposable
        {
            private readonly SessionIdPool sessions;
            private readonly MemoryAllocator<byte>? bufferPool;
            internal readonly int Capacity;
            internal readonly DataAccessSession WriteSession, CompactionSession;

            internal DataAccessSessionManager(int readersCount, MemoryAllocator<byte>? sharedPool, int writeBufferSize)
            {
                Capacity = readersCount;
                bufferPool = sharedPool;
                sessions = readersCount <= FastSessionIdPool.MaxReadersCount ? new FastSessionIdPool() : new SlowSessionIdPool(readersCount);
                WriteSession = new DataAccessSession(0, bufferPool, writeBufferSize);
                CompactionSession = new DataAccessSession(1, bufferPool, writeBufferSize);
            }

            internal DataAccessSession OpenSession(int bufferSize)
                => new DataAccessSession(sessions.Take(), bufferPool, bufferSize);

            internal void CloseSession(in DataAccessSession readSession)
            {
                readSession.Dispose();
                sessions.Return(readSession.SessionId);
            }

            public void Dispose()
            {
                WriteSession.Dispose();
                CompactionSession.Dispose();
            }
        }

        // concurrent read sessions management
        private readonly DataAccessSessionManager sessionManager;
    }
}
