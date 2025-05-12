using System.Buffers;
using System.Runtime.InteropServices;
using DotNext.IO;

namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using IO;

partial class PersistentState
{
    [StructLayout(LayoutKind.Auto)]
    protected readonly struct LogEntry : IRaftLogEntry
    {
        private readonly ReadOnlySequence<byte> buffer;

        /// <inheritdoc/>
        bool IDataTransferObject.IsReusable => true;

        public long Length => buffer.Length;

        /// <inheritdoc/>
        long? IDataTransferObject.Length => Length;
        
        public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token) 
            where TWriter : IAsyncBinaryWriter
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset Timestamp { get; }
        public long Term { get; }
    }
}