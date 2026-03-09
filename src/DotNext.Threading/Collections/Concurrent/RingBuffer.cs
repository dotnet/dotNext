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
    private bool frozenForEnqueues;
    private State state;
    
    public RingBuffer(int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)maximumRetained));
        slots = new Slot[length];
        indexMask = length - 1U;
        indexBits = int.CreateChecked(nuint.Log2(length));
        frozenForEnqueues = false;
    }

    public readonly bool IsFrozen => Volatile.Read(in frozenForEnqueues);

    public void Freeze()
    {
        if (Interlocked.FalseToTrue(ref frozenForEnqueues))
            WaitForPendingEnqueues();
    }
    
    private void WaitForPendingEnqueues()
    {
        Debug.Assert(frozenForEnqueues);

        // the slots prior the frozen position can be still in progress by the enqueuer, so wait for it
        for (var position = FreezeProducer() - 1U;
             this[position & indexMask].WaitForPendingEnqueue(position >> indexBits);
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

    public ref Slot TryDequeue(out nuint sequence) => ref DoOperation<DequeueOperation>(ref state.Consumer, out sequence);

    public readonly bool IsEmpty
    {
        get
        {
            var position = state.Consumer;
            var generation = position >> indexBits;

            return this[position & indexMask].Sequence != (generation | StateBit);
        }
    }

    public ref Slot TryEnqueue(out nuint sequence) => ref DoOperation<EnqueueOperation>(ref state.Producer, out sequence);

    private readonly ref Slot DoOperation<TOperation>(scoped ref nuint position, out nuint newSeq)
        where TOperation : struct, IOperation<TOperation>, allows ref struct
    {
        var spinner = new SpinWait();
        for (nuint positionCopy = Volatile.Read(in position), tmp; TOperation.Retry(in frozenForEnqueues); positionCopy = tmp)
        {
            ref var slot = ref this[positionCopy & indexMask];
            var context = TOperation.Create(positionCopy >>> indexBits);
            var slotSeq = slot.Sequence;

            if (!context.IsValidSequence(slotSeq))
            {
                if (!TOperation.CanRetry(in state, positionCopy))
                    break;

                tmp = positionCopy + 1U;
                spinner.SpinOnce(sleep1Threshold: -1);
            }
            else if ((tmp = Interlocked.CompareExchange(ref position, positionCopy + 1U, positionCopy)) == positionCopy)
            {
                newSeq = context.NextSequence;
                return ref slot;
            }
        }

        Unsafe.SkipInit(out newSeq);
        return ref Unsafe.NullRef<Slot>();
    }

    [StructLayout(LayoutKind.Auto)]
    public struct Slot
    {
        public T? Item;
        public volatile nuint Sequence; // higher bit is reserved for the value presence

        public readonly bool WaitForPendingEnqueue(nuint frozenGen)
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
    
    private interface IOperation<out TSelf>
        where TSelf : struct, IOperation<TSelf>, allows ref struct
    {
        static abstract bool Retry(ref readonly bool frozenForEnqueues);

        static abstract bool CanRetry(ref readonly State state, nuint position);

        nuint NextSequence { get; }

        bool IsValidSequence(nuint sequence);

        public static abstract TSelf Create(nuint generation);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct EnqueueOperation : IOperation<EnqueueOperation>
    {
        private readonly nuint generation;

        private EnqueueOperation(nuint generation) => this.generation = generation;

        static bool IOperation<EnqueueOperation>.CanRetry(ref readonly State state, nuint position)
        {
            var consumerPos = Volatile.Read(in state.Consumer);
            return consumerPos >= position;
        }
        
        static bool IOperation<EnqueueOperation>.Retry(ref readonly bool frozenForEnqueues) => !frozenForEnqueues;

        // the slot becomes available for consumption in the current generation
        nuint IOperation<EnqueueOperation>.NextSequence => generation | StateBit;

        bool IOperation<EnqueueOperation>.IsValidSequence(nuint sequence) => sequence == generation;

        static EnqueueOperation IOperation<EnqueueOperation>.Create(nuint generation) => new(generation);
        
        public override string ToString() => generation.ToString("X");
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct DequeueOperation : IOperation<DequeueOperation>
    {
        private readonly nuint generation;
        
        private DequeueOperation(nuint generation) => this.generation = generation;
        
        static bool IOperation<DequeueOperation>.Retry(ref readonly bool frozenForEnqueues) => true;
        
        static bool IOperation<DequeueOperation>.CanRetry(ref readonly State state, nuint position)
            => position != Volatile.Read(in state.Producer);

        // the slot can be occupied in the next generation only
        nuint IOperation<DequeueOperation>.NextSequence => (generation + 1U) & ~StateBit;

        bool IOperation<DequeueOperation>.IsValidSequence(nuint sequence) => sequence == (generation | StateBit);

        static DequeueOperation IOperation<DequeueOperation>.Create(nuint generation) => new(generation);

        public override string ToString() => generation.ToString("X");
    }
}

// producer/consumer positions are used by different threads, so it's better to avoid memory cache sharing
// between threads, because both of them are updated with CompareExchange which forces the cache invalidation
[StructLayout(LayoutKind.Explicit, Size = CacheLineSize * 3, Pack = sizeof(ulong))]
internal struct State
{
    private const int CacheLineSize = 128;

    [FieldOffset(1 * CacheLineSize)] public nuint Producer;
    [FieldOffset(2 * CacheLineSize)] public nuint Consumer;
}