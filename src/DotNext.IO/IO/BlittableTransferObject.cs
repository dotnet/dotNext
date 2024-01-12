using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers.Binary;
using Runtime.Serialization;

/// <summary>
/// Represents a value of blittable type as DTO.
/// </summary>
/// <typeparam name="T">The blittable type.</typeparam>
public class BlittableTransferObject<T> : MemoryManager<byte>, ISerializable<BlittableTransferObject<T>>, IBinaryFormattable<BlittableTransferObject<T>>
    where T : unmanaged
{
    private T value;
    private T[]? pinnedBuffer;

    /// <inheritdoc/>
    bool IDataTransferObject.IsReusable => true;

    /// <inheritdoc/>
    unsafe long? IDataTransferObject.Length => sizeof(T);

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => writer.Invoke(Memory, token);

    /// <inheritdoc/>
    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => transformation.TransformAsync<SequenceReader>(new(Memory), token);

    /// <summary>
    /// Gets the content of this object.
    /// </summary>
    public ref T Content => ref pinnedBuffer is null ? ref value : ref MemoryMarshal.GetArrayDataReference(pinnedBuffer);

    /// <inheritdoc/>
    public sealed override Span<byte> GetSpan()
    {
        ref var valueRef = ref pinnedBuffer is null
            ? ref value
            : ref MemoryMarshal.GetArrayDataReference(pinnedBuffer);

        return Span.AsBytes(ref valueRef);
    }

    /// <inheritdoc/>
    public sealed override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        ref var elementRef = ref Unsafe.NullRef<T>();
        if (pinnedBuffer is null)
        {
            pinnedBuffer = GC.AllocateArray<T>(length: 1, pinned: true);
            elementRef = ref pinnedBuffer[elementIndex];
            elementRef = value;
        }
        else
        {
            elementRef = ref pinnedBuffer[elementIndex];
        }

        return new(Unsafe.AsPointer(ref elementRef));
    }

    /// <inheritdoc/>
    public sealed override void Unpin()
    {
    }

    /// <inheritdoc cref="IBinaryFormattable{T}.Size"/>
    static unsafe int IBinaryFormattable<BlittableTransferObject<T>>.Size => sizeof(T);

    /// <inheritdoc/>
    void IBinaryFormattable<BlittableTransferObject<T>>.Format(Span<byte> output)
        => GetSpan().CopyTo(output);

    /// <inheritdoc cref="IBinaryFormattable{T}.Parse(ReadOnlySpan{byte})"/>
    static BlittableTransferObject<T> IBinaryFormattable<BlittableTransferObject<T>>.Parse(ReadOnlySpan<byte> source)
    {
        var result = new BlittableTransferObject<T>();
        var destination = result.GetSpan();
        source = source.Slice(0, destination.Length);
        source.CopyTo(destination);
        return result;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            pinnedBuffer = null;
        }
    }

    /// <inheritdoc cref="ISerializable{TSelf}.ReadFromAsync{TReader}(TReader, CancellationToken)"/>
    static ValueTask<BlittableTransferObject<T>> ISerializable<BlittableTransferObject<T>>.ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        => reader.ReadAsync<BlittableTransferObject<T>>(token);
}