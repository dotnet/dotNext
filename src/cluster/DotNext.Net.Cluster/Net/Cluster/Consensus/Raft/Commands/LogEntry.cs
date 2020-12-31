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
    /// <typeparam name="TCommand">The type of the command encoded by the log entry.</typeparam>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct LogEntry<TCommand> : IRaftLogEntry
        where TCommand : struct
    {
        private readonly TCommand command;
        private readonly ICommandFormatter<TCommand> formatter;
        private readonly int id;

        internal LogEntry(long term, TCommand command, ICommandFormatter<TCommand> formatter, int id)
        {
            Term = term;
            Timestamp = DateTimeOffset.Now;
            this.command = command;
            this.formatter = formatter;
            this.id = id;
        }

        /// <inheritdoc />
        public long Term { get; }

        /// <inheritdoc />
        public DateTimeOffset Timestamp { get; }

        /// <inheritdoc />
        bool IDataTransferObject.IsReusable => true;

        /// <inheritdoc />
        long? IDataTransferObject.Length
        {
            get
            {
                var result = formatter.GetLength(in command);
                if (result.TryGetValue(out var length))
                    result = new long?(length + sizeof(int));

                return result;
            }
        }

        /// <inheritdoc />
        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            await writer.WriteInt32Async(id, true, token).ConfigureAwait(false);
            await formatter.SerializeAsync(command, writer, token).ConfigureAwait(false);
        }
    }
}