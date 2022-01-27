using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;
using IO.Log;

public partial class PersistentState
{
    [StructLayout(LayoutKind.Auto)]
    internal struct CacheRecord : IDisposable
    {
        internal MemoryOwner<byte> Content;
        internal bool Persisted;

        public void Dispose() => Content.Dispose();
    }

    /// <summary>
    /// Represents buffered Raft log entry.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CachedLogEntry : IRaftLogEntry
    {
        private readonly CacheRecord record;

        internal bool PersistenceRequired
        {
            get => record.Persisted;
            init => record.Persisted = value;
        }

        internal MemoryOwner<byte> Content
        {
            get => record.Content;
            init => record.Content = value;
        }

        public long Term { get; init; }

        public int? CommandId { get; init; }

        internal long Length => record.Content.Length;

        long? IDataTransferObject.Length => Length;

        bool ILogEntry.IsSnapshot => false;

        public DateTimeOffset Timestamp { get; init; }

        bool IDataTransferObject.IsReusable => true;

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteAsync(record.Content.Memory, null, token);

        ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
            => transformation.TransformAsync(IAsyncBinaryReader.Create(record.Content.Memory), token);

        bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        {
            memory = record.Content.Memory;
            return true;
        }

        public static implicit operator CacheRecord(in CachedLogEntry entry) => entry.record;
    }
}