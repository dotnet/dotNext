using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft
{
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
            internal CachedLogEntry(IMemoryOwner<byte> content, long term, DateTimeOffset timestamp, int? commandId)
            {
                Content = content;
                Term = term;
                Timestamp = timestamp;
                CommandId = commandId;
            }

            internal IMemoryOwner<byte> Content { get; }

            public long Term { get; }

            public int? CommandId { get;  }

            internal long Length => Content.Memory.Length;

            long? IDataTransferObject.Length => Length;

            bool ILogEntry.IsSnapshot => false;

            public DateTimeOffset Timestamp { get; }

            bool IDataTransferObject.IsReusable => true;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(Content.Memory, null, token);

            ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                => transformation.TransformAsync(IAsyncBinaryReader.Create(Content.Memory), token);

            bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
            {
                memory = Content.Memory;
                return true;
            }
        }
    }
}