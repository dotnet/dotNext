using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using static Runtime.Intrinsics;

    public partial class CommandInterpreter
    {
        /// <summary>
        /// Represents builder of the interpreter.
        /// </summary>
        public sealed class Builder : IConvertible<CommandInterpreter>
        {
            private readonly Dictionary<int, CommandHandler> interpreters;
            private readonly Dictionary<Type, object> formatters;

            /// <summary>
            /// Initializes a new builder.
            /// </summary>
            public Builder()
            {
                interpreters = new Dictionary<int, CommandHandler>();
                formatters = new Dictionary<Type, object>();
            }

            /// <summary>
            /// Registers command handler.
            /// </summary>
            /// <param name="handler">The command handler.</param>
            /// <param name="formatter">Serializer/deserializer of the command type.</param>
            /// <typeparam name="TCommand">The type of the command supported by the handler.</typeparam>
            /// <exception cref="ArgumentNullException"><paramref name="handler"/> or <paramref name="formatter"/> is <see langword="null"/>.</exception>
            public void Add<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler, ICommandFormatter<TCommand> formatter)
                where TCommand : struct 
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));
                if (formatter is null)
                    throw new ArgumentNullException(nameof(formatter));

                var id = typeof(TCommand).GetCustomAttribute<CommandAttribute>()?.Id ?? throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandAttribute<TCommand>());
                interpreters.Add(id, new CommandHandler<TCommand>(formatter, handler));
                formatters.Add(typeof(TCommand), formatter);
            }

            /// <summary>
            /// Registers command handler.
            /// </summary>
            /// <param name="handler">The command handler.</param>
            /// <typeparam name="TCommand">The type of the command supported by the handler.</typeparam>
            /// <exception cref="ArgumentNullException"><paramref name="handler"/> is <see langword="null"/>.</exception>
            public void Add<TCommand>(Func<TCommand, CancellationToken, ValueTask> handler)
                where TCommand : struct
            {
                if (handler is null)
                    throw new ArgumentNullException(nameof(handler));

                var attr = typeof(TCommand).GetCustomAttribute<CommandAttribute>();
                if (attr is null || attr.Formatter is null)
                    throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandAttribute<TCommand>());

                var formatter = Activator.CreateInstance(attr.Formatter) ?? throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandAttribute<TCommand>());
                var interp = Activator.CreateInstance(typeof(CommandHandler<>).MakeGenericType(typeof(TCommand)), formatter, handler);
                interpreters.Add(attr.Id, Cast<CommandHandler>(interp));
            }

            /// <summary>
            /// Clears this builder so it can be reused.
            /// </summary>
            public void Clear()
            {
                interpreters.Clear();
                formatters.Clear();
            }

            /// <summary>
            /// Constructs an instance of <see cref="CommandInterpreter"/>.
            /// </summary>
            /// <returns>A new instance of the interpreter.</returns>
            public CommandInterpreter Build() => new CommandInterpreter(interpreters, formatters);

            /// <inheritdoc />
            CommandInterpreter IConvertible<CommandInterpreter>.Convert() => Build();
        }
    }
}