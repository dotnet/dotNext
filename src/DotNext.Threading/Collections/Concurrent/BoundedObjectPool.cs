using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

internal sealed class BoundedObjectPool<T> : IObjectPool<T>
    where T : class
{
    private readonly Slot[] slots;
    private readonly nuint indexMask;
    private readonly int indexBits;
    private State state;

    public BoundedObjectPool(int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)maximumRetained));
        slots = new Slot[length];
        indexMask = length - 1U;
        indexBits = (int)nuint.Log2(length);
    }

    private static nuint StateBit => (nuint)nint.MinValue;

    public int Capacity => slots.Length;

    private ref Slot this[nuint index]
    {
        get
        {
            Debug.Assert(index < (uint)slots.Length);

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), index);
        }
    }

    T? IObjectPool<T>.TryRent()
    {
        var result = default(T);
        for (var spinner = new SpinWait();; spinner.SpinOnce(sleep1Threshold: -1))
        {
            var position = state.Consumer;
            var newPosition = position + 1U;
            var index = position & indexMask;
            ref var slot = ref this[index];

            if (slot.Sequence != ((position >>> indexBits) | StateBit))
                break;

            if (Interlocked.CompareExchange(ref state.Consumer, position + 1U, position) == position)
            {
                result = slot.Item;
                slot.Sequence = newPosition >>> indexBits;
                break;
            }
        }

        return result;
    }

    void IObjectPool<T>.Return(T item)
    {
        for (var spinner = new SpinWait();; spinner.SpinOnce(sleep1Threshold: -1))
        {
            var position = state.Producer;
            var newPosition = position + 1U;
            var index = position & indexMask;
            ref var slot = ref this[index];

            if (slot.Sequence != position >>> indexBits)
                break;

            if (Interlocked.CompareExchange(ref state.Producer, newPosition, position) == position)
            {
                slot.Item = item;
                slot.Sequence = (newPosition >>> indexBits) | StateBit;
                break;
            }
        }
    }

    [StructLayout(LayoutKind.Auto)]
    private struct Slot
    {
        public T? Item;
        public volatile nuint Sequence; // higher bit is reserved for the value presence
    }

    // producer/consumer positions are used by different threads, so it's better to avoid memory cache sharing
    // between the threads, because both of them are updated with CompareExchange which forces cache invalidation
    [StructLayout(LayoutKind.Sequential)]
    private struct State
    {
        private Padding128 Prologue;
        public volatile nuint Producer;
        
        private Padding128 Middle;
        
        public volatile nuint Consumer;
        private Padding128 Epilogue;
    }

    [InlineArray(128 / sizeof(ulong))]
    private struct Padding128
    {
        private ulong element0;
    }
}