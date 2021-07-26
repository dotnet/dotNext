using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;
    using Runtime.Serialization;

    /// <summary>
    /// Represents Raft log entry containing custom command.
    /// </summary>
    /// <typeparam name="TCommand">The type of the command encoded by the log entry.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LogEntry<TCommand> : IRaftLogEntry
        where TCommand : struct
    {
        private readonly IFormatter<TCommand> formatter;
        private readonly int id;

        internal LogEntry(long term, TCommand command, IFormatter<TCommand> formatter, int id)
        {
            Term = term;
            Timestamp = DateTimeOffset.Now;
            Command = command;
            this.formatter = formatter;
            this.id = id;
        }

        /// <summary>
        /// Gets the command associated with this log entry.
        /// </summary>
        public TCommand Command { get; }

        /// <inheritdoc />
        public long Term { get; }

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        int? IRaftLogEntry.CommandId => id;

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        long? IDataTransferObject.Length
        {
            get
            {
                var result = formatter.GetLength(Command);
                if (result.TryGetValue(out var length))
                    result = new long?(length + sizeof(int));

                return result;
            }
        }

        /// <inheritdoc />
        ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => formatter.SerializeAsync(Command, writer, token);
    }
}