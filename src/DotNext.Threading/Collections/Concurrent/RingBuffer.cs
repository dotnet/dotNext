using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

// Inspired by Dmitry Vyukov’s MPMC queue
[StructLayout(LayoutKind.Auto)]
internal struct RingBuffer<T>
{
    private readonly Slot[] slots;
    private readonly nuint indexMask;
    private readonly int indexBits;
    private State state;
    
    public RingBuffer(int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)maximumRetained));
        slots = new Slot[length];
        indexMask = length - 1U;
        indexBits = (int)nuint.Log2(length);
    }

    public readonly int Length => slots.Length;
    
    private static nuint StateBit => (nuint)nint.MinValue;
    
    private readonly ref Slot this[nuint index]
    {
        get
        {
            Debug.Assert(index < (uint)slots.Length);

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), index);
        }
    }

    public ref Slot TryDequeue(out nuint sequence)
    {
        for (var spinner = new SpinWait();; spinner.SpinOnce(sleep1Threshold: -1))
        {
            var position = state.Consumer;
            var index = position & indexMask;
            ref var slot = ref this[index];
            var generation = position >>> indexBits;

            if (slot.Sequence != (generation | StateBit))
                break;

            if (Interlocked.CompareExchange(ref state.Consumer, position + 1U, position) == position)
            {
                // the slot becomes available for write in the next generation
                sequence = (generation + 1U) & ~StateBit;
                return ref slot;
            }
        }

        sequence = 0;
        return ref Unsafe.NullRef<Slot>();
    }

    public ref Slot TryEnqueue(out nuint sequence)
    {
        for (var spinner = new SpinWait();; spinner.SpinOnce(sleep1Threshold: -1))
        {
            var position = state.Producer;
            var index = position & indexMask;
            ref var slot = ref this[index];
            var generation = position >>> indexBits;

            if (slot.Sequence != generation)
                break;

            if (Interlocked.CompareExchange(ref state.Producer, position + 1, position) == position)
            {
                // the slot becomes available for consumption in the current generation
                sequence = generation | StateBit;
                return ref slot;
            }
        }

        sequence = 0;
        return ref Unsafe.NullRef<Slot>();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Slot
    {
        public T? Item;
        public volatile nuint Sequence; // higher bit is reserved for the value presence
    }

    // producer/consumer positions are used by different threads, so it's better to avoid memory cache sharing
    // between threads, because both of them are updated with CompareExchange which forces the cache invalidation
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