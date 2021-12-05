using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Versioning;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO.Log;
using Runtime.Serialization;
using static Reflection.MethodExtensions;
using static Runtime.Intrinsics;

/// <summary>
/// Represents interpreter of the log entries.
/// </summary>
/// <remarks>
/// The interpreter can be constructed in two ways: using <see cref="CommandInterpreter.Builder"/>
/// and through inheritance. If you choose the inheritance then command handlers must be declared
/// as public instance methods marked with <see cref="CommandInterpreter.CommandHandlerAttribute"/> attribute.
/// Otherwise, command handlers can be registered through the builder.
/// Typically, the interpreter is aggregated by the class derived from <see cref="PersistentState"/>.
/// All command types must be registered using <see cref="CommandAttribute{TCommand}"/> attributes
/// applied to the derived type.
/// </remarks>
[RequiresPreviewFeatures]
public partial class CommandInterpreter : Disposable
{
    /// <summary>
    /// Indicates that the command cannot be decoded correctly.
    /// </summary>
    public sealed class UnknownCommandException : IntegrityException
    {
        internal UnknownCommandException(int id)
            : base(ExceptionMessages.UnknownCommand(id))
        {
            CommandId = id;
        }

        /// <summary>
        /// Gets ID of the unrecognized command.
        /// </summary>
        public int CommandId { get; }
    }

    private readonly IHandlerRegistry interpreters;
    private readonly IReadOnlyDictionary<Type, int> identifiers;
    private readonly int? snapshotCommandId;

    /// <summary>
    /// Initializes a new interpreter and discovers methods marked
    /// with <see cref="CommandHandlerAttribute"/> attribute.
    /// </summary>
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors, typeof(CommandHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(Func<,>))]
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    protected CommandInterpreter()
    {
        // explore command types
        var identifiers = new Dictionary<Type, int>();
        foreach (var attribute in GetType().GetCustomAttributes<CommandAttribute>(true))
        {
            var type = attribute.GetType();

            if (type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(CommandAttribute<>))
            {
                type = type.GetGenericArguments()[0];
                identifiers.Add(type, attribute.Id);
            }
        }

        // register interpreters
        var interpreters = new Dictionary<int, CommandHandler>();
        const BindingFlags publicInstanceMethod = BindingFlags.Public | BindingFlags.Instance;
        foreach (var method in GetType().GetMethods(publicInstanceMethod))
        {
            var handlerAttr = method.GetCustomAttribute<CommandHandlerAttribute>();
            if (handlerAttr is not null && method.ReturnType == typeof(ValueTask))
            {
                var parameters = method.GetParameterTypes();
                if (GetLength(parameters) != 2 || !parameters[0].IsValueType || parameters[1] != typeof(CancellationToken))
                    continue;
                var commandType = parameters[0];
                if (!identifiers.TryGetValue(commandType, out var commandId))
                    continue;

                var interpreter = Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(commandType, typeof(CancellationToken), typeof(ValueTask)), method.IsStatic ? null : this, method);
                interpreters.Add(commandId, Cast<CommandHandler>(Activator.CreateInstance(typeof(CommandHandler<>).MakeGenericType(commandType), interpreter)));

                if (handlerAttr.IsSnapshotHandler)
                    snapshotCommandId = commandId;
            }
        }

        this.interpreters = CreateRegistry(interpreters);
        interpreters.Clear();

        identifiers.TrimExcess();
        this.identifiers = identifiers;
    }

    private CommandInterpreter(IDictionary<int, CommandHandler> interpreters, IDictionary<Type, int> identifiers, int? snapshotCommandId)
    {
        this.interpreters = CreateRegistry(interpreters);
        this.identifiers = ImmutableDictionary.ToImmutableDictionary(identifiers);
        this.snapshotCommandId = snapshotCommandId;
    }

    /// <summary>
    /// Wraps the command to the log entry.
    /// </summary>
    /// <param name="command">The payload of the command.</param>
    /// <param name="term">The term of the local node.</param>
    /// <typeparam name="TCommand">The type of the command.</typeparam>
    /// <returns>The instance of the log entry containing the command.</returns>
    /// <exception cref="GenericArgumentException"><typeparamref name="TCommand"/> is not registered with <see cref="CommandAttribute{TCommand}"/>.</exception>
    public LogEntry<TCommand> CreateLogEntry<TCommand>(TCommand command, long term)
        where TCommand : notnull, ISerializable<TCommand>
        => identifiers.TryGetValue(typeof(TCommand), out var id) ?
            new LogEntry<TCommand>(term, command, id) :
            throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandId, nameof(command));

    private bool TryGetCommandId<TEntry>(ref TEntry entry, out int commandId)
        where TEntry : struct, IRaftLogEntry
        => (entry.IsSnapshot ? snapshotCommandId : entry.CommandId).TryGetValue(out commandId);

    /// <summary>
    /// Interprets log entry asynchronously.
    /// </summary>
    /// <remarks>
    /// Typically this method is called by the custom implementation of
    /// <see cref="MemoryBasedStateMachine.ApplyAsync(PersistentState.LogEntry)"/> method.
    /// </remarks>
    /// <param name="entry">The log entry to be interpreted.</param>
    /// <param name="token">The token that can be used to cancel the interpretation.</param>
    /// <typeparam name="TEntry">The type of the log entry to be interpreted.</typeparam>
    /// <returns>The ID of the interpreted log entry.</returns>
    /// <exception cref="UnknownCommandException">The command handler was not registered for the command represented by <paramref name="entry"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<int> InterpretAsync<TEntry>(TEntry entry, CancellationToken token = default)
        where TEntry : struct, IRaftLogEntry
        => TryGetCommandId(ref entry, out var id) ?
            entry.TransformAsync<int, InterpretingTransformation>(new InterpretingTransformation(id, interpreters), token) :
            ValueTask.FromException<int>(new ArgumentException(ExceptionMessages.MissingCommandId, nameof(entry)));
}