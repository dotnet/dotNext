using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Messaging
{
    /// <summary>
    /// Represents typed client for sending messages to the nodes in the cluster.
    /// </summary>
    public partial class MessagingClient
    {
        private readonly IOutputChannel channel;
        private readonly ConcurrentDictionary<Type, InputMessageFactory> inputs;
        private readonly ConcurrentDictionary<Type, MulticastDelegate> outputs;

        /// <summary>
        /// Constructs a new typed client for messaging.
        /// </summary>
        /// <param name="channel">The output channel for outbound messages.</param>
        /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
        public MessagingClient(IOutputChannel channel)
        {
            this.channel = channel ?? throw new ArgumentNullException(nameof(channel));
            inputs = new();
            outputs = new();
        }

        /// <summary>
        /// Sends a request message.
        /// </summary>
        /// <typeparam name="TInput">The type of the request.</typeparam>
        /// <typeparam name="TOutput">The type of the response.</typeparam>
        /// <param name="input">The request.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The response.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task<TOutput> SendMessageAsync<TInput, TOutput>(TInput input, CancellationToken token = default)
        {
            Task<TOutput> result;
            try
            {
                result = channel.SendMessageAsync(CreateMessage<TInput>(input), GetMessageReader<TOutput>(), token);
            }
            catch (Exception e)
            {
                result = Task.FromException<TOutput>(e);
            }

            return result;
        }

        /// <summary>
        /// Sends one-way message.
        /// </summary>
        /// <typeparam name="TInput">The type of the message.</typeparam>
        /// <param name="input">The message to send.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public Task SendSignalAsync<TInput>(TInput input, CancellationToken token = default)
        {
            Task result;
            try
            {
                result = channel.SendSignalAsync(CreateMessage<TInput>(input), token);
            }
            catch (Exception e)
            {
                result = Task.FromException(e);
            }

            return result;
        }

        private Message<TInput> CreateMessage<TInput>(TInput payload)
        {
            var key = typeof(TInput);

            if (inputs.TryGetValue(key, out var untyped) is false || untyped is not InputMessageFactory<TInput> factory)
            {
                inputs[key] = factory = new();
            }

            return factory.CreateMessage(payload);
        }

        private MessageReader<TOutput> GetMessageReader<TOutput>()
        {
            var key = typeof(TOutput);

            if (outputs.TryGetValue(key, out var untyped) is false || untyped is not MessageReader<TOutput> reader)
            {
                outputs[key] = reader = CreateReader<TOutput>();
            }

            return reader;
        }
    }
}