using System;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents No-OP entry.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct EmptyEntry : IRaftLogEntry
    {
        /// <summary>
        /// Initializes a new empty log entry.
        /// </summary>
        /// <param name="term">The term value.</param>
        public EmptyEntry(long term)
        {
            Term = term;
            Timestamp = DateTimeOffset.UtcNow;
        }

        long? IDataTransferObject.Length => 0;

        bool IDataTransferObject.IsReusable => true;

        /// <summary>
        /// Gets or sets log entry term.
        /// </summary>
        public long Term { get; }

        /// <summary>
        /// Gets timestamp of this log entry.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token) => new ValueTask();

        ValueTask<TResult> IDataTransferObject.GetObjectDataAsync<TResult, TDecoder>(TDecoder parser, CancellationToken token) => parser.ReadAsync(IAsyncBinaryReader.Empty, token);
    }
}
