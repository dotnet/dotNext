using System.Runtime.CompilerServices;

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

    private sealed class CommandHandler<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler) : CommandHandler
        where TCommand : notnull, ISerializable<TCommand>
    {
        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        internal override async ValueTask InterpretAsync<TReader>(TReader reader, CancellationToken token)
        {
            var command = await TCommand.ReadFromAsync(reader, token).ConfigureAwait(false);
            await handler(command, token).ConfigureAwait(false);
        }
    }
}