namespace DotNext.IO;

/// <summary>
/// Represents object which content is represented by <see cref="Stream"/>.
/// </summary>
public class StreamTransferObject : Disposable, IDataTransferObject, IAsyncDisposable
{
    private readonly bool leaveOpen;
    private readonly Stream content;

    /// <summary>
    /// Initializes a new message.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="leaveOpen"><see langword="true"/> to leave the stream open after <see cref="StreamTransferObject"/> object is disposed; otherwise, <see langword="false"/>.</param>
    public StreamTransferObject(Stream content, bool leaveOpen)
    {
        this.leaveOpen = leaveOpen;
        this.content = content;
    }

    /// <summary>
    /// Loads the content from another data transfer object.
    /// </summary>
    /// <param name="source">The content source.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous state of content loading.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="NotSupportedException">The underlying stream does not support seeking.</exception>
    public async ValueTask LoadFromAsync(IDataTransferObject source, CancellationToken token = default)
    {
        if (content is { CanWrite: true, CanSeek: true })
        {
            try
            {
                await source.WriteToAsync(content, token: token).ConfigureAwait(false);
            }
            finally
            {
                content.Seek(0, SeekOrigin.Begin);
            }
        }
        else
        {
            throw new NotSupportedException();
        }
    }

    /// <summary>
    /// Indicates that the content of this message can be copied to the output stream or pipe multiple times.
    /// </summary>
    public virtual bool IsReusable => content.CanSeek;

    /// <inheritdoc/>
    long? IDataTransferObject.Length => content.CanSeek ? content.Length : default(long?);

    /// <inheritdoc/>
    async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        try
        {
            await writer.CopyFromAsync(content, token).ConfigureAwait(false);
        }
        finally
        {
            if (IsReusable)
                content.Seek(0, SeekOrigin.Begin);
        }
    }

    /// <summary>
    /// Parses the encapsulated stream.
    /// </summary>
    /// <param name="transformation">The parser instance.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <typeparam name="TTransformation">The type of parser.</typeparam>
    /// <returns>The converted DTO content.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token = default)
        where TTransformation : IDataTransferObject.ITransformation<TResult>
        => IDataTransferObject.TransformAsync<TResult, TTransformation>(content, transformation, IsReusable, token);

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        if (content is MemoryStream ms && ms.TryGetBuffer(out var buffer))
        {
            memory = buffer;
            return true;
        }

        memory = default;
        return false;
    }

    /// <summary>
    /// Releases resources associated with this object.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> if called from <see cref="Disposable.Dispose()"/>; <see langword="false"/> if called from finalizer <see cref="Disposable.Finalize()"/>.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !leaveOpen)
            content.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore()
        => leaveOpen ? ValueTask.CompletedTask : content.DisposeAsync();

    /// <summary>
    /// Asynchronously releases the resources associated with this object.
    /// </summary>
    /// <returns>A task that represents the asynchronous dispose operation.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}