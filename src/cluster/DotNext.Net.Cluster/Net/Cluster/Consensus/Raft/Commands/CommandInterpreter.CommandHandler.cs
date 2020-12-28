using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using IO;

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
            private readonly ICommandFormatter<TCommand> formatter;
            private readonly Func<TCommand, CancellationToken, ValueTask> handler;

            public CommandHandler(ICommandFormatter<TCommand> formatter, Func<TCommand, CancellationToken, ValueTask> handler)
            {
                this.formatter = formatter;
                this.handler = handler;
            }

            public CommandHandler(FormatterInfo formatter, Func<TCommand, CancellationToken, ValueTask> handler)
            {
                if (!formatter.TryGetFormatter<TCommand>(out var fmt))
                    throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandAttribute<TCommand>(), nameof(formatter));
                this.formatter = fmt;
                this.handler = handler;
            }

            internal override async ValueTask InterpretAsync<TReader>(TReader reader, CancellationToken token)
            {
                var command = await formatter.DeserializeAsync(reader, token).ConfigureAwait(false);
                await handler(command, token).ConfigureAwait(false);
            }
        }
    }
}