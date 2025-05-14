using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Runtime;
using IO.Log;
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
            => CommandId = id;

        /// <summary>
        /// Gets ID of the unrecognized command.
        /// </summary>
        public int CommandId { get; }
    }

    private readonly IHandlerRegistry interpreters;
    private readonly int? snapshotCommandId;

    /// <summary>
    /// Initializes a new interpreter and discovers methods marked
    /// with <see cref="CommandHandlerAttribute"/> attribute.
    /// </summary>
    [RequiresUnreferencedCode("Dynamic code generation may be incompatible with IL trimming")]
    [RequiresDynamicCode("Runtime binding requires dynamic code compilation")]
    protected CommandInterpreter()
    {
        // register interpreters
        var interpreters = new Dictionary<int, CommandHandler>();
        const BindingFlags publicInstanceMethod = BindingFlags.Public | BindingFlags.Instance;
        var handlerAttributeType = typeof(CommandHandlerAttribute);
        foreach (var method in GetType().GetMethods(publicInstanceMethod))
        {
            if (method.IsDefined(handlerAttributeType) && method.ReturnType == typeof(ValueTask))
            {
                var parameters = method.GetParameterTypes();
                Delegate interpreter;
                switch (parameters.GetLength())
                {
                    case 2 when parameters[0].IsValueType && parameters[1] == typeof(CancellationToken):
                        interpreter = Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(parameters[0], parameters[1], typeof(ValueTask)),
                            method.IsStatic ? null : this, method);
                        break;
                    case 3 when parameters[0].IsValueType && parameters[1] == typeof(object) && parameters[2] == typeof(CancellationToken):
                        interpreter = Delegate.CreateDelegate(
                            typeof(Func<,,,>).MakeGenericType(parameters[0], parameters[1], parameters[2], typeof(ValueTask)),
                            method.IsStatic ? null : this, method);
                        break;
                    default:
                        continue;
                }

                if (!TryGetCommandId(parameters[0], out var commandId, out var isSnapshotHandler))
                    continue;

                interpreters.Add(commandId,
                    Cast<CommandHandler>(Activator.CreateInstance(typeof(CommandHandler<>).MakeGenericType(parameters[0]), interpreter)));

                if (isSnapshotHandler)
                    snapshotCommandId = commandId;
            }

            static bool TryGetCommandId(Type commandType, out int commandId, out bool isSnapshotHandler)
            {
                isSnapshotHandler = false;
                commandId = 0;
                var commandInterface = typeof(ICommand<>);
                foreach (var iface in commandType.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == commandInterface && iface.GetGenericArguments()[0] == commandType)
                    {
                        var mapping = commandType.GetInterfaceMap(iface);
                        for (var i = 0; i < mapping.InterfaceMethods.Length; i++)
                        {
                            switch (mapping.InterfaceMethods[i].Name)
                            {
                                case "get_Id" when mapping.TargetMethods[i].Invoke(obj: null, parameters: null) is int value:
                                    commandId = value;
                                    break;
                                case "get_IsSnapshot" when mapping.TargetMethods[i].Invoke(obj: null, parameters: null) is bool value:
                                    isSnapshotHandler = value;
                                    break;
                            }
                        }

                        return true;
                    }
                }

                return false;
            }
        }

        this.interpreters = CreateRegistry(interpreters);
        interpreters.Clear();
    }

    private CommandInterpreter(IDictionary<int, CommandHandler> interpreters, int? snapshotCommandId)
    {
        this.interpreters = CreateRegistry(interpreters);
        this.snapshotCommandId = snapshotCommandId;
    }

    private bool TryGetCommandId<TEntry>(ref TEntry entry, out int commandId)
        where TEntry : IRaftLogEntry
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
        where TEntry : IRaftLogEntry
        => InterpretAsync(entry, context: null, token);

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
        where TEntry : IRaftLogEntry
        => TryGetCommandId(ref entry, out var id) ?
            entry.TransformAsync<int, InterpretingTransformation>(new InterpretingTransformation(id, interpreters) { Context = context }, token) :
            ValueTask.FromException<int>(new ArgumentException(ExceptionMessages.MissingCommandId, nameof(entry)));
}