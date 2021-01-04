using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;
    using Runtime.Serialization;

    public partial class CommandInterpreter
    {
        private abstract class CommandHandler
        {
            internal abstract ValueTask InterpretAsync<TReader>(TReader reader, CancellationToken token)
                where TReader : notnull, IAsyncBinaryReader;
        }

        private sealed class CommandHandler<TCommand> : CommandHandler
            where TCommand : struct
        {
            private readonly IFormatter<TCommand> formatter;
            private readonly Func<TCommand, CancellationToken, ValueTask> handler;

            public CommandHandler(IFormatter<TCommand> formatter, Func<TCommand, CancellationToken, ValueTask> handler)
            {
                this.formatter = formatter;
                this.handler = handler;
            }

            public CommandHandler(FormatterInfo formatter, Func<TCommand, CancellationToken, ValueTask> handler)
                : this(formatter.GetFormatter<TCommand>(), handler)
            {
            }

            internal override async ValueTask InterpretAsync<TReader>(TReader reader, CancellationToken token)
            {
                var command = await formatter.DeserializeAsync(reader, token).ConfigureAwait(false);
                await handler(command, token).ConfigureAwait(false);
            }
        }
    }
}