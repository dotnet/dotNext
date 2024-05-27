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
        internal CachedLogEntryPersistenceMode PersistenceMode;
        internal object? Context;

        public void Dispose()
        {
            Context = null;
            Content.Dispose();
        }
    }

    internal enum CachedLogEntryPersistenceMode : byte
    {
        None = 0,
        CopyToBuffer,
        SkipBuffer,
    }

    /// <summary>
    /// Represents buffered Raft log entry.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    internal readonly struct CachedLogEntry : IInputLogEntry
    {
        private readonly CacheRecord record;

        public object? Context
        {
            get => record.Context;
            init => record.Context = value;
        }

        internal CachedLogEntryPersistenceMode PersistenceMode
        {
            get => record.PersistenceMode;
            init => record.PersistenceMode = value;
        }

        required internal MemoryOwner<byte> Content
        {
            get => record.Content;
            init => record.Content = value;
        }

        required public long Term { get; init; }

        required public int? CommandId { get; init; }

        internal long Length => record.Content.Length;

        long? IDataTransferObject.Length => Length;

        bool ILogEntry.IsSnapshot => false;

        required public DateTimeOffset Timestamp { get; init; }

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