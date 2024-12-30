using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using Runtime.Serialization;

public partial class CommandInterpreter
{
    private abstract class CommandHandler
    {
        internal abstract ValueTask InterpretAsync<TReader>(TReader reader, object? context, CancellationToken token)
            where TReader : IAsyncBinaryReader;
    }

    private sealed class CommandHandler<TCommand>(Func<TCommand, object?, CancellationToken, ValueTask> handler) : CommandHandler
        where TCommand : ISerializable<TCommand>
    {
        public CommandHandler(Func<TCommand, CancellationToken, ValueTask> handler)
            : this(handler.Invoke<TCommand>)
        {
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        internal override async ValueTask InterpretAsync<TReader>(TReader reader, object? context, CancellationToken token)
        {
            var command = await TCommand.ReadFromAsync(reader, token).ConfigureAwait(false);
            await handler(command, context, token).ConfigureAwait(false);
        }
    }
}

file static class CommandHandlerExtensions
{
    public static ValueTask Invoke<TCommand>(this Func<TCommand, CancellationToken, ValueTask> handler, TCommand command, object? context, CancellationToken token)
        => handler.Invoke(command, token);
}