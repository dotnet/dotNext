using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Threading;

// Inspired by Dmitry Vyukov’s MPMC queue
[StructLayout(LayoutKind.Auto)]
internal struct RingBuffer<T>
{
    private readonly Slot[] slots;
    private readonly nuint indexMask;
    private readonly int indexBits;
    private State state;
    private bool frozenForEnqueues;
    
    public RingBuffer(int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)maximumRetained));
        slots = new Slot[length];
        indexMask = length - 1U;
        indexBits = (int)nuint.Log2(length);
        frozenForEnqueues = false;
    }

    public readonly bool IsFrozen => Volatile.Read(in frozenForEnqueues);

    public void Freeze()
    {
        if (Interlocked.FalseToTrue(ref frozenForEnqueues))
            FreezeAndWait();
    }
    
    private void FreezeAndWait()
    {
        Debug.Assert(frozenForEnqueues);

        // the slots prior the frozen position can be still in progress by the enqueuer, so wait for it
        for (var position = FreezeProducer() - 1U;
             this[position & indexMask].WaitForEnqueuedState(position >> indexBits);
             position--) ;
    }

    private nuint FreezeProducer()
    {
        Debug.Assert(frozenForEnqueues);
        
        const nuint shift = 2U;
        var current = state.Producer;
        for (nuint tmp, offset = (uint)slots.Length * shift;; current = tmp)
        {
            // Advances the producer too far forward, so this position cannot be reached naturally even
            // if the buffer is full. 2 generations forward is enough, because the items in the buffer
            // can be in the current and the next generation.
            tmp = Interlocked.CompareExchange(ref state.Producer, current + offset, current);
            if (tmp == current)
                break;
        }

        return current;
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
        for (var spinner = new SpinWait(); !frozenForEnqueues; spinner.SpinOnce(sleep1Threshold: -1))
        {
            var position = state.Producer;
            var index = position & indexMask;
            ref var slot = ref this[index];
            var generation = position >>> indexBits;

            if (slot.Sequence != generation)
                break;

            if (Interlocked.CompareExchange(ref state.Producer, position + 1U, position) == position)
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

        public readonly bool WaitForEnqueuedState(nuint frozenGen)
        {
            // The slot can be in three states:
            // 1. Enqueued, so Sequence == (frozenGen | StateBit) => skip it and check the previous slot
            // 2. Dequeued, so Sequence == (frozenGen + 1) & ~StateBit => leave the method
            // 3. In-flight, so Sequence == frozenGen => wait for Enqueued or Dequeued
            nuint sequence;
            for (var spinner = new SpinWait();
                 (sequence = Sequence) == frozenGen;
                 spinner.SpinOnce()) ;

            return sequence == (frozenGen | StateBit);
        }
    }
}

// producer/consumer positions are used by different threads, so it's better to avoid memory cache sharing
// between threads, because both of them are updated with CompareExchange which forces the cache invalidation
[StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 3, Pack = sizeof(ulong))]
internal struct State
{
    private const int CacheLineSize = 128;

    [FieldOffset(1 * CacheLineSize)] public volatile nuint Producer;
    [FieldOffset(2 * CacheLineSize)] public volatile nuint Consumer;
}