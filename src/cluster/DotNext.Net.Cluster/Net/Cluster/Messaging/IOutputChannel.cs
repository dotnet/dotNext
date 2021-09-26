namespace DotNext.Net.Cluster.Messaging;

/// <summary>
/// Defines the interface that a channel must implement to send a message.
/// </summary>
public interface IOutputChannel
{
    /// <summary>
    /// Sends a request message.
    /// </summary>
    /// <remarks>
    /// The message content may be available inside of <paramref name="responseReader"/> only.
    /// Do not try to return <see cref="IMessage">response message</see> itself from the delegate.
    /// </remarks>
    /// <typeparam name="TResponse">The type of the parsed response message.</typeparam>
    /// <param name="message">The message to be sent.</param>
    /// <param name="responseReader">The response reader.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The message representing response; or <see langword="null"/> if request message in one-way.</returns>
    /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task<TResponse> SendMessageAsync<TResponse>(IMessage message, MessageReader<TResponse> responseReader, CancellationToken token = default);

    /// <summary>
    /// Sends one-way message.
    /// </summary>
    /// <param name="message">The message to be sent.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing execution of this method.</returns>
    /// <exception cref="InvalidOperationException">Attempts to send message to local or unavailable endpoint.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    Task SendSignalAsync(IMessage message, CancellationToken token = default);
}