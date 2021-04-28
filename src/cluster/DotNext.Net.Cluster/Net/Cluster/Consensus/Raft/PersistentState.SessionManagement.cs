using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;
    using AtomicBoolean = Threading.AtomicBoolean;

    public partial class PersistentState
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct DataAccessSession
        {
            internal readonly int SessionId;
            internal readonly Memory<byte> Buffer;

            internal DataAccessSession(int sessionId, Memory<byte> buffer)
            {
                SessionId = sessionId;
                Buffer = buffer;
            }
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
        // and represents concurrent power set.
        // TODO: Expand to 64 readers in .NET 6 (.NET Standard doesnt CMPXCHG for ulong data type)
        private sealed class FastSessionIdPool : SessionIdPool
        {
            internal const int MaxReadersCount = 31;

#if NETSTANDARD2_1
            // https://github.com/dotnet/roslyn/pull/24621
            private static ReadOnlySpan<byte> TrailingZeroCountDeBruijn => new byte[]
            {
                00, 01, 28, 02, 29, 14, 24, 03,
                30, 22, 20, 15, 25, 17, 04, 08,
                31, 27, 13, 23, 21, 19, 16, 07,
                26, 12, 18, 06, 11, 05, 10, 09,
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
                ref var first = ref MemoryMarshal.GetReference(TrailingZeroCountDeBruijn);
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
                var sessionId = (Environment.CurrentManagedThreadId & int.MaxValue) % tokens.Length;
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
        private struct DataAccessSessionManager : IDisposable
        {
            private readonly SessionIdPool sessions;
            private readonly int bufferSize;
            internal readonly int Capacity;
            internal readonly DataAccessSession WriteSession, CompactionSession;
            private MemoryOwner<byte> writeBuffer, compactionBuffer, readBuffer;

            internal DataAccessSessionManager(int readersCount, MemoryAllocator<byte> sharedPool, int bufferSize)
            {
                Capacity = readersCount;
                sessions = readersCount <= FastSessionIdPool.MaxReadersCount ? new FastSessionIdPool() : new SlowSessionIdPool(readersCount);

                writeBuffer = sharedPool.Invoke(bufferSize, false);
                WriteSession = new(0, writeBuffer.Memory);

                compactionBuffer = sharedPool.Invoke(bufferSize, false);
                CompactionSession = new(1, compactionBuffer.Memory);

                readBuffer = sharedPool.Invoke(checked(readersCount * bufferSize), false);
                this.bufferSize = bufferSize;
            }

            internal readonly DataAccessSession OpenSession()
            {
                var id = sessions.Take();
                Debug.Assert(id >= 0 && id < Capacity);

                // renting buffer for read session is trivial here:
                // just compute offset in a shared buffer for all readers
                return new DataAccessSession(id, readBuffer.Memory.Slice(bufferSize * id, bufferSize));
            }

            internal readonly void CloseSession(in DataAccessSession readSession)
                => sessions.Return(readSession.SessionId);

            public void Dispose()
            {
                writeBuffer.Dispose();
                writeBuffer = default;

                compactionBuffer.Dispose();
                compactionBuffer = default;

                readBuffer.Dispose();
                readBuffer = default;
            }
        }

        // concurrent read sessions management
        private DataAccessSessionManager sessionManager;
    }
}
