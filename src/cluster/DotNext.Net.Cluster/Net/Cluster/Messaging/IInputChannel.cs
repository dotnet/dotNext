namespace DotNext.Net.Cluster.Messaging;

/// <summary>
/// Defines the interface that a channel must implement to receive a message.
/// </summary>
public interface IInputChannel
{
    /// <summary>
    /// Determines whether the specified message can be processed by this handler.
    /// </summary>
    /// <param name="messageName">The name of the message.</param>
    /// <param name="oneWay"><see langword="true"/> if message is one-way; <see langword="false"/> if message is request message that requires a response.</param>
    /// <returns><see langword="true"/> if message can be processed by this handler; otherwise, <see langword="false"/>.</returns>
    bool IsSupported(string messageName, bool oneWay) => true;

    /// <summary>
    /// Handles incoming message from the specified cluster member.
    /// </summary>
    /// <remarks>
    /// Implementation of this method should handle every exception inside of it
    /// and prepare response message representing such exception.
    /// </remarks>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="message">The received message.</param>
    /// <param name="context">The context of the underlying network request.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The response message.</returns>
    Task<IMessage> ReceiveMessage(ISubscriber sender, IMessage message, object? context, CancellationToken token);

    /// <summary>
    /// Handles incoming signal from the specified cluster member.
    /// </summary>
    /// <param name="sender">The sender of the message.</param>
    /// <param name="signal">The received message representing signal.</param>
    /// <param name="context">The context of the underlying network request.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of the method.</returns>
    Task ReceiveSignal(ISubscriber sender, IMessage signal, object? context, CancellationToken token);
}