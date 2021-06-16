using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using Messaging;

    public partial class CommandInterpreter
    {
        /// <summary>
        /// Creates a message that encapsulated the command.
        /// </summary>
        /// <typeparam name="TCommand">The type of the command.</typeparam>
        /// <param name="command">The command.</param>
        /// <param name="mimeType">MIME type of the message.</param>
        /// <returns>The message encapsulating the command.</returns>
        /// <exception cref="GenericArgumentException">
        /// <typeparamref name="TCommand"/> type doesn't have registered <see cref="Runtime.Serialization.IFormatter{TCommand}"/>;
        /// or <typeparamref name="TCommand"/> type is not marked with <see cref="CommandAttribute"/>.
        /// </exception>
        public IMessage CreateMessage<TCommand>(TCommand command, string? mimeType = null)
            where TCommand : struct
        {
            if (!formatters.TryGetValue(typeof(TCommand), out var info))
                throw new GenericArgumentException<TCommand>(ExceptionMessages.MissingCommandId, nameof(command));

            return new Message<TCommand>(info.Id.ToString(InvariantCulture), command, info.GetFormatter<TCommand>(), mimeType);
        }
    }
}