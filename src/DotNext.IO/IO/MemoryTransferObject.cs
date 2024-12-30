namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents data transfer object holding the content of the predefined size in the memory.
/// </summary>
/// <param name="length">The length, in bytes, of the content.</param>
/// <param name="allocator">The memory allocator.</param>
public class MemoryTransferObject(int length, MemoryAllocator<byte>? allocator = null) : Disposable, IDataTransferObject
{
    private MemoryOwner<byte> owner = length > 0 ? allocator.AllocateExactly(length) : default;

    /// <summary>
    /// Transforms this object to serialized form.
    /// </summary>
    /// <param name="writer">The binary writer.</param>
    /// <param name="token">The toke that can be used to cancel the operation.</param>
    /// <typeparam name="TWriter">The type of writer.</typeparam>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
        => IsDisposed ? new(DisposedTask) : writer.WriteAsync(Content, null, token);

    /// <summary>
    /// Converts data transfer object to another type.
    /// </summary>
    /// <param name="transformation">The parser instance.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <typeparam name="TTransformation">The type of parser.</typeparam>
    /// <returns>The converted DTO content.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token = default)
        where TTransformation : IDataTransferObject.ITransformation<TResult>
        => IsDisposed ? new(GetDisposedTask<TResult>()) : transformation.TransformAsync(new SequenceReader(Content), token);

    /// <summary>
    /// Gets the content of this object.
    /// </summary>
    public Memory<byte> Content => owner.Memory;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    long? IDataTransferObject.Length => owner.Length;

    /// <inheritdoc/>
    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
    {
        memory = Content;
        return true;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            owner.Dispose();
        }

        base.Dispose(disposing);
    }
}