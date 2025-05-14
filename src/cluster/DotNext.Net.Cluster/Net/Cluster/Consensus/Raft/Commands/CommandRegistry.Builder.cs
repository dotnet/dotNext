namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Patterns;
using Runtime.Serialization;

public partial class CommandInterpreter : IBuildable<CommandInterpreter, CommandInterpreter.Builder>
{
    /// <summary>
    /// Represents builder of the interpreter.
    /// </summary>
    public sealed class Builder : ISupplier<CommandInterpreter>, IResettable
    {
        private readonly Dictionary<int, CommandHandler> interpreters = new();
        private int? snapshotCommandId;

        private Builder Add<TCommand>(CommandHandler<TCommand> handler)
            where TCommand : ICommand<TCommand>
        {
            interpreters.Add(TCommand.Id, handler);
            if (TCommand.IsSnapshot)
                snapshotCommandId = TCommand.Id;

            return this;
        }

        /// <summary>
        /// Registers command handler.
        /// </summary>
        /// <param name="handler">The command handler.</param>
        /// <typeparam name="TCommand">The type of the command supported by the handler.</typeparam>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
        public Builder Add<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler)
            where TCommand : ICommand<TCommand>
        {
            ArgumentNullException.ThrowIfNull(handler);

            return Add(new CommandHandler<TCommand>(handler));
        }

        /// <summary>
        /// Registers command handler.
        /// </summary>
        /// <param name="handler">The command handler.</param>
        /// <typeparam name="TCommand">The type of the command supported by the handler.</typeparam>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
        public Builder Add<TCommand>(Func<TCommand, object?, CancellationToken, ValueTask> handler)
            where TCommand : ICommand<TCommand>
        {
            ArgumentNullException.ThrowIfNull(handler);

            return Add(new CommandHandler<TCommand>(handler));
        }

        /// <summary>
        /// Clears this builder so it can be reused.
        /// </summary>
        public void Reset()
        {
            interpreters.Clear();
        }

        /// <summary>
        /// Constructs an instance of <see cref="CommandInterpreter"/>.
        /// </summary>
        /// <returns>A new instance of the interpreter.</returns>
        public CommandInterpreter Build() => new(interpreters, snapshotCommandId);

        /// <inheritdoc/>
        CommandInterpreter ISupplier<CommandInterpreter>.Invoke() => Build();
    }

    /// <inheritdoc cref="IBuildable{TSelf, TBuilder}.CreateBuilder"/>
    static Builder IBuildable<CommandInterpreter, Builder>.CreateBuilder() => new();
}