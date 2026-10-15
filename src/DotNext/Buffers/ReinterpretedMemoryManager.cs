using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime.CompilerServices;

internal sealed class ReinterpretedMemoryManager<T> : MemoryManager<T>
{
    private const long SmallBufferThreshold = 128;
    private int length;
    private byte[] data;

    private ReinterpretedMemoryManager(int sizeInBytes)
    {
        Debug.Assert(sizeInBytes > 0);
        Debug.Assert(sizeInBytes % Unsafe.SizeOf<T>() is 0);
        Debug.Assert(!RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        Debug.Assert(Unsafe.IsNaturallyAligned<T>());

        data = ArrayPool<byte>.Shared.Rent(sizeInBytes);
        length = data.Length / Unsafe.SizeOf<T>();
    }

    public static MemoryOwner<T> Rent(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);

        MemoryOwner<T> result;
        var sizeInBytes = (long)Unsafe.SizeOf<T>() * length;
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            || !Unsafe.IsNaturallyAligned<T>()
            || typeof(T) == typeof(byte)
            || sizeInBytes <= SmallBufferThreshold
            || sizeInBytes > Array.MaxLength)
        {
            result = new(ArrayPool<T>.Shared, length, exactSize: false);
        }
        else
        {
            var manager = new ReinterpretedMemoryManager<T>(int.CreateTruncating(sizeInBytes));
            result = new(new MemoryOwner<T>.Ownership(manager), manager.length);
        }

        return result;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && data is not [])
        {
            ArrayPool<byte>.Shared.Return(data);
            data = [];
            length = 0;
        }
    }

    public override Span<T> GetSpan()
    {
        ref var firstElement = ref Unsafe.As<byte, T>(ref MemoryMarshal.GetArrayDataReference(data));
        return MemoryMarshal.CreateSpan(ref firstElement, length);
    }

    public override Memory<T> Memory => CreateMemory(length);

    protected override bool TryGetArray(out ArraySegment<T> segment)
    {
        segment = default;
        return false;
    }

    public override unsafe MemoryHandle Pin(int elementIndex = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)elementIndex, (uint)length, nameof(elementIndex));

        var offset = Unsafe.SizeOf<T>() * elementIndex;
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        var pointer = Unsafe.AsPointer(ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(data), offset));
        return new(pointer, handle);
    }

    public override void Unpin()
    {
        // nothing to do here
    }
}