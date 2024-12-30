using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using DotNext.Runtime;
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
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    protected CommandInterpreter()
    {
        // explore command types
        var identifiers = GetType().GetCustomAttributes<CommandAttribute>(true).ToDictionary(static attr => attr.CommandType, static attr => attr.Id);

        // register interpreters
        var interpreters = new Dictionary<int, CommandHandler>();
        const BindingFlags publicInstanceMethod = BindingFlags.Public | BindingFlags.Instance;
        foreach (var method in GetType().GetMethods(publicInstanceMethod))
        {
            var handlerAttr = method.GetCustomAttribute<CommandHandlerAttribute>();
            if (handlerAttr is not null && method.ReturnType == typeof(ValueTask))
            {
                var parameters = method.GetParameterTypes();
                Delegate interpreter;
                switch (parameters.GetLength())
                {
                    case 2 when parameters[0].IsValueType && parameters[1] == typeof(CancellationToken):
                        interpreter = Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(parameters[0], parameters[1], typeof(ValueTask)), method.IsStatic ? null : this, method);
                        break;
                    case 3 when parameters[0].IsValueType && parameters[1] == typeof(object) && parameters[2] == typeof(CancellationToken):
                        interpreter = Delegate.CreateDelegate(typeof(Func<,,,>).MakeGenericType(parameters[0], parameters[1], parameters[2], typeof(ValueTask)), method.IsStatic ? null : this, method);
                        break;
                    default:
                        continue;
                }

                if (!identifiers.TryGetValue(parameters[0], out var commandId))
                    continue;

                interpreters.Add(commandId, Cast<CommandHandler>(Activator.CreateInstance(typeof(CommandHandler<>).MakeGenericType(parameters[0]), interpreter)));

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
        this.identifiers = identifiers.ToFrozenDictionary();
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
        where TCommand : ISerializable<TCommand>
        => identifiers.TryGetValue(typeof(TCommand), out var id) ?
            new() { Term = term, Command = command, CommandId = id } :
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

    /// <summary>
    /// Interprets log entry asynchronously.
    /// </summary>
    /// <remarks>
    /// Typically this method is called by the custom implementation of
    /// <see cref="MemoryBasedStateMachine.ApplyAsync(PersistentState.LogEntry)"/> method.
    /// </remarks>
    /// <param name="entry">The log entry to be interpreted.</param>
    /// <param name="context">The context to be passed to the handler.</param>
    /// <param name="token">The token that can be used to cancel the interpretation.</param>
    /// <typeparam name="TEntry">The type of the log entry to be interpreted.</typeparam>
    /// <returns>The ID of the interpreted log entry.</returns>
    /// <exception cref="UnknownCommandException">The command handler was not registered for the command represented by <paramref name="entry"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<int> InterpretAsync<TEntry>(TEntry entry, object? context, CancellationToken token = default)
        where TEntry : struct, IRaftLogEntry
        => TryGetCommandId(ref entry, out var id) ?
            entry.TransformAsync<int, InterpretingTransformation>(new InterpretingTransformation(id, interpreters) { Context = context }, token) :
            ValueTask.FromException<int>(new ArgumentException(ExceptionMessages.MissingCommandId, nameof(entry)));
}