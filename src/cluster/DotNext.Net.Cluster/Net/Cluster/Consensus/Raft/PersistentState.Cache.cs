using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

public partial class PersistentState
{
    /// <summary>
    /// Represents buffered Raft log entry.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CachedLogEntry : IRaftLogEntry
    {
        private readonly MemoryOwner<byte> content;

        internal CachedLogEntry(in MemoryOwner<byte> content, long term, DateTimeOffset timestamp, int? commandId)
        {
            this.content = content;
            Term = term;
            Timestamp = timestamp;
            CommandId = commandId;
        }

        internal MemoryOwner<byte> Content => content;

        public long Term { get; }

        public int? CommandId { get; }

        internal long Length => content.Length;

        long? IDataTransferObject.Length => Length;

        bool ILogEntry.IsSnapshot => false;

        public DateTimeOffset Timestamp { get; }

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(content.Memory, null, token);

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => transformation.TransformAsync(IAsyncBinaryReader.Create(content.Memory), token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = content.Memory;
            return true;
        }
    }
}