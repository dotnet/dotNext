namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

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
        where TCommand : notnull, ISerializable<TCommand>
    {
        private readonly Func<TCommand, CancellationToken, ValueTask> handler;

        public CommandHandler(Func<TCommand, CancellationToken, ValueTask> handler)
        {
            this.handler = handler;
        }

        internal override async ValueTask InterpretAsync<TReader>(TReader reader, CancellationToken token)
        {
            var command = await TCommand.ReadFromAsync(reader, token).ConfigureAwait(false);
            await handler(command, token).ConfigureAwait(false);
        }
    }
}