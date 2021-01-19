using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using Buffers;

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

        /*
         * This class helps to organize thread-safe concurrent access to the multiple streams
         * used for reading log entries. Such approach allows to use one-writer multiple-reader scenario
         * which dramatically improves the performance
         */
        [StructLayout(LayoutKind.Auto)]
        private readonly struct DataAccessSessionManager : IDisposable
        {
            private readonly ConcurrentBag<int>? tokens;
            internal readonly int Capacity;
            private readonly MemoryAllocator<byte>? bufferPool;
            internal readonly DataAccessSession WriteSession;

            internal DataAccessSessionManager(int readersCount, MemoryAllocator<byte>? sharedPool, int writeBufferSize)
            {
                Capacity = readersCount;
                bufferPool = sharedPool;
                tokens = readersCount == 1 ? null : new ConcurrentBag<int>(Enumerable.Range(0, readersCount));
                WriteSession = new DataAccessSession(0, bufferPool, writeBufferSize);
            }

            internal DataAccessSession OpenSession(int bufferSize)
            {
                if (tokens is null)
                    return WriteSession;
                if (tokens.TryTake(out var sessionId))
                    return new DataAccessSession(sessionId, bufferPool, bufferSize);

                // never happens
                throw new InternalBufferOverflowException(ExceptionMessages.NoAvailableReadSessions);
            }

            internal void CloseSession(in DataAccessSession readSession)
            {
                if (tokens is null)
                    return;
                tokens.Add(readSession.SessionId);
                readSession.Dispose();
            }

            public void Dispose() => WriteSession.Dispose();
        }

        // concurrent read sessions management
        private readonly DataAccessSessionManager sessionManager;
    }
}
