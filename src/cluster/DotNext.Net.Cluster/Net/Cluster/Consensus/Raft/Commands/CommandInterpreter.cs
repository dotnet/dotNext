using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using static Reflection.MethodExtensions;
    using static Runtime.Intrinsics;
    using IntegrityException = IO.Log.IntegrityException;

    /// <summary>
    /// Represents interpreter of the log entries.
    /// </summary>
    /// <remarks>
    /// The interpreter can be constructed in two ways: using <see cref="CommandInterpreter.Builder"/>
    /// and through inheritance. If you choose the inheritance then command handlers can be declared
    /// as public methods marked with <see cref="CommandInterpreter.CommandHandlerAttribute"/> attribute.
    /// Otherwise, command handlers can be registered through builder.
    /// Typically, the interpreter is aggregated by the class derived from <see cref="PersistentState"/>.
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
        private readonly IReadOnlyDictionary<Type, object> formatters;

        /// <summary>
        /// Initializes a new interpreter and discovers methods marked
        /// with <see cref="CommandHandlerAttribute"/> attribute.
        /// </summary>
        protected CommandInterpreter()
        {
            var interpreters = new Dictionary<int, CommandHandler>();
            var formatters = new Dictionary<Type, object>();
            foreach (var method in GetType().GetMethods())
            {
                var handlerAttr = method.GetCustomAttribute<CommandHandlerAttribute>();
                if (handlerAttr is not null && method.ReturnType == typeof(ValueTask))
                {
                    var parameters = method.GetParameterTypes();
                    if (GetLength(parameters) != 2 || !parameters[0].IsValueType || parameters[1] != typeof(CancellationToken))
                        continue;
                    var commandType = parameters[0];
                    var commandAttr = commandType.GetCustomAttribute<CommandAttribute>();
                    if (commandAttr is null || commandAttr.Formatter is null)
                        continue;
                    var formatter = Activator.CreateInstance(commandAttr.Formatter);
                    Debug.Assert(formatter is not null);
                    var interpreter = Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(commandType, typeof(CancellationToken), typeof(ValueTask)), method.IsStatic ? null : this, method);
                    Debug.Assert(interpreter is not null);
                    interpreters.Add(commandAttr.Id, Cast<CommandHandler>(Activator.CreateInstance(typeof(CommandHandler<>).MakeGenericType(commandType), formatter, interpreter)));
                    formatters.Add(commandType, formatter);
                }
            }

            this.interpreters = CreateRegistry(interpreters);
            interpreters.Clear();

            formatters.TrimExcess();
            this.formatters = formatters;
        }

        private CommandInterpreter(IDictionary<int, CommandHandler> interpreters, IDictionary<Type, object> formatters)
        {
            this.interpreters = CreateRegistry(interpreters);
            this.formatters = ImmutableDictionary.ToImmutableDictionary(formatters);
        }

        /// <summary>
        /// Wraps the command to the log entry.
        /// </summary>
        /// <param name="command">The payload of the command.</param>
        /// <param name="term">The term of the local node.</param>
        /// <typeparam name="TCommand">The type of the command.</typeparam>
        /// <returns>The instance of the log entry containing the command.</returns>
        /// <exception cref="GenericArgumentException"><typeparamref name="TCommand"/> type doesn't have registered <see cref="ICommandFormatter{TCommand}"/>.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public LogEntry<TCommand> CreateLogEntry<TCommand>(TCommand command, long term)
            where TCommand : struct
            => formatters.TryGetValue(typeof(TCommand), out var formatter) && formatter is ICommandFormatter<TCommand> typedFormatter ?
                new LogEntry<TCommand>(term, command, typedFormatter) :
                throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandFormatter<TCommand>(), nameof(command));

        /// <summary>
        /// Interprets log entry asynchronously.
        /// </summary>
        /// <remarks>
        /// Typically this method is called by the custom implementation of
        /// <see cref="PersistentState.ApplyAsync(PersistentState.LogEntry)"/> method.
        /// </remarks>
        /// <param name="entry">The log entry to be interpreted.</param>
        /// <param name="token">The token that can be used to cancel the interpretation.</param>
        /// <typeparam name="TEntry">The type of the log entry to be interpreted.</typeparam>
        /// <returns>The ID of the interpreted log entry.</returns>
        /// <exception cref="UnknownCommandException">The command handler was not registered for the command represented by <paramref name="entry"/>.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<int> InterpretAsync<TEntry>(TEntry entry, CancellationToken token = default)
            where TEntry : struct, IRaftLogEntry
            => entry.GetObjectDataAsync<int, IHandlerRegistry>(interpreters, token);
    }
}