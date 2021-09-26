namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Runtime.Serialization;

public partial class CommandInterpreter
{
    /// <summary>
    /// Represents builder of the interpreter.
    /// </summary>
    public sealed class Builder : ISupplier<CommandInterpreter>
    {
        private readonly Dictionary<int, CommandHandler> interpreters;
        private readonly Dictionary<Type, int> identifiers;
        private int? snapshotCommandId;

        /// <summary>
        /// Initializes a new builder.
        /// </summary>
        public Builder()
        {
            interpreters = new();
            identifiers = new();
        }

        /// <summary>
        /// Registers command handler.
        /// </summary>
        /// <param name="commandId">The identifier of the command.</param>
        /// <param name="handler">The command handler.</param>
        /// <param name="snapshotHandler">
        /// <see langword="true"/> to register a handler for snapshot log entry;
        /// <see langword="false"/> to register a handler for regular log entry.
        /// </param>
        /// <typeparam name="TCommand">The type of the command supported by the handler.</typeparam>
        /// <returns>This builder.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
        /// <exception cref="GenericArgumentException">Type <typaparamref name="TCommand"/> is not annotated with <see cref="CommandAttribute"/> attribute.</exception>
        public Builder Add<TCommand>(int commandId, Func<TCommand, CancellationToken, ValueTask> handler, bool snapshotHandler = false)
            where TCommand : notnull, ISerializable<TCommand>
        {
            if (handler is null)
                throw new ArgumentNullException(nameof(handler));

            identifiers.Add(typeof(TCommand), commandId);
            interpreters.Add(commandId, new CommandHandler<TCommand>(handler));
            if (snapshotHandler)
                snapshotCommandId = commandId;
            return this;
        }

        /// <summary>
        /// Clears this builder so it can be reused.
        /// </summary>
        public void Clear()
        {
            interpreters.Clear();
            identifiers.Clear();
        }

        /// <summary>
        /// Constructs an instance of <see cref="CommandInterpreter"/>.
        /// </summary>
        /// <returns>A new instance of the interpreter.</returns>
        public CommandInterpreter Build() => new(interpreters, identifiers, snapshotCommandId);

        /// <inheritdoc />
        CommandInterpreter ISupplier<CommandInterpreter>.Invoke() => Build();
    }
}