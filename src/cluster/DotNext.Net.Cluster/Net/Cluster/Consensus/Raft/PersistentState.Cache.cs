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
        private readonly struct CachedRaftLogEntry : IRaftLogEntry
        {
            private readonly long term;
            private readonly int? commandId;
            private readonly DateTimeOffset timestamp;

            internal CachedRaftLogEntry(IMemoryOwner<byte> content, long term, DateTimeOffset timestamp, int? commandId)
            {
                Content = content;
                this.term = term;
                this.timestamp = timestamp;
                this.commandId = commandId;
            }

            internal IMemoryOwner<byte> Content { get; }

            long IRaftLogEntry.Term => term;

            int? IRaftLogEntry.CommandId => commandId;

            long? IDataTransferObject.Length => Content.Memory.Length;

            bool ILogEntry.IsSnapshot => false;

            DateTimeOffset ILogEntry.Timestamp => timestamp;

            bool IDataTransferObject.IsReusable => true;

            ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
                => writer.WriteAsync(Content.Memory, null, token);

            ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
                => transformation.TransformAsync(IAsyncBinaryReader.Create(Content.Memory), token);
        }
    }
}