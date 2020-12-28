using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;

    /// <summary>
    /// Represents Raft log entry containing custom command.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LogEntry<TCommand> : IRaftLogEntry
        where TCommand : struct
    {
        private readonly TCommand command;
        private readonly ICommandFormatter<TCommand> formatter;

        internal LogEntry(long term, TCommand command, ICommandFormatter<TCommand> formatter)
        {
            Term = term;
            Timestamp = DateTimeOffset.Now;
            this.command = command;
            this.formatter = formatter;
        }

        /// <inheritdoc />
        public long Term { get; }

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        long? IDataTransferObject.Length => formatter.GetLength(in command);

        /// <inheritdoc />
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => formatter.SerializeAsync(command, writer, token);
    }
}