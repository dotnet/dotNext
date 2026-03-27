using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

using Threading;

// Inspired by Dmitry Vyukov’s MPMC queue
[StructLayout(LayoutKind.Sequential)]
internal struct RingBuffer<T>
{
    private readonly Slot[] slots;
    private readonly nuint indexMask;
    private readonly int indexBits;
    private bool frozenForEnqueues;
    private State state;

    public RingBuffer(int desiredSize)
    {
        Debug.Assert(desiredSize > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)desiredSize));
        slots = new Slot[length];
        indexMask = length - 1U;
        indexBits = int.CreateChecked(nuint.Log2(length));
        frozenForEnqueues = false;
    }
    
    public bool Freeze()
    {
        var frozen = Interlocked.FalseToTrue(ref frozenForEnqueues);
        if (frozen)
            WaitForPendingEnqueues();

        return frozen;
    }

    private void WaitForPendingEnqueues()
    {
        Debug.Assert(IsFrozen);

        // the slots prior to the frozen position can be still in progress by the enqueuer, so wait for it
        for (var position = FreezeProducer() - 1U;
             this[GetIndex(position)].WaitForPendingEnqueue(GetGeneration(position));
             position--) ;
    }

    private nuint FreezeProducer()
    {
        Debug.Assert(IsFrozen);
        
        const nuint shift = 2U;
        var current = state.Producer;
        for (nuint tmp, offset = Array.GetLength(slots) * shift;; current = tmp)
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
    
    private static nuint StateBit => (nuint)nint.MinValue;

    public readonly bool IsFrozen => Volatile.Read(in frozenForEnqueues);

    public readonly int Length => slots.Length;
    
    private readonly ref Slot this[nuint index]
    {
        get
        {
            Debug.Assert(index < (uint)slots.Length);

            return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(slots), index);
        }
    }

    public ref Slot TryDequeue(out nuint sequence)
        => ref DoOperation<DequeueOperation>(out sequence);

    public readonly bool IsEmpty
    {
        get
        {
            var position = Consumer;
            var generation = GetGeneration(position);

            return this[GetIndex(position)].Sequence != (generation | StateBit);
        }
    }

    public ref Slot TryEnqueue(out nuint sequence)
        => ref DoOperation<EnqueueOperation>(out sequence);
    
    private ref Slot DoOperation<TOperation>(out nuint newSeq)
        where TOperation : struct, IOperation<TOperation>, allows ref struct
    {
        var spinner = new SpinWait();
        for (nuint positionCopy = TOperation.GetPosition(ref state), tmp; TOperation.Retry(in frozenForEnqueues); positionCopy = tmp)
        {
            ref var slot = ref this[GetIndex(positionCopy)];
            var context = TOperation.Create(GetGeneration(positionCopy));

            if (!context.IsValidSequence(slot.Sequence))
            {
                if (!TOperation.CanRetry(in this, positionCopy))
                    break;

                tmp = positionCopy + 1U;
                spinner.SpinOnce(sleep1Threshold: -1);
            }
            else if ((tmp = Interlocked.CompareExchange(ref TOperation.GetPosition(ref state), positionCopy + 1U, positionCopy)) == positionCopy)
            {
                newSeq = context.NextSequence;
                return ref slot;
            }
        }

        Unsafe.SkipInit(out newSeq);
        return ref Unsafe.NullRef<Slot>();
    }
    
    private readonly nuint GetGeneration(nuint position) => position >>> indexBits;

    private readonly nuint GetIndex(nuint position) => position & indexMask;
    
    private interface IOperation
    {
        static abstract ref nuint GetPosition(ref State state);
        
        static abstract bool Retry(ref readonly bool frozenForEnqueues);

        static abstract bool CanRetry(ref readonly RingBuffer<T> state, nuint position);

        nuint NextSequence { get; }

        bool IsValidSequence(nuint sequence);
    }

    private interface IOperation<out TSelf> : IOperation
        where TSelf : struct, IOperation<TSelf>, allows ref struct
    {
        public static abstract TSelf Create(nuint generation);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct EnqueueOperation : IOperation<EnqueueOperation>
    {
        private readonly nuint generation;

        private EnqueueOperation(nuint generation) => this.generation = generation;

        static bool IOperation.CanRetry(ref readonly RingBuffer<T> state, nuint producerPosition)
            => producerPosition != state.ConsumerNextGen;

        static bool IOperation.Retry(ref readonly bool frozenForEnqueues) => !Volatile.Read(in frozenForEnqueues);

        // the slot becomes available for consumption in the current generation
        nuint IOperation.NextSequence => generation | StateBit;

        bool IOperation.IsValidSequence(nuint sequence) => sequence == generation;

        static EnqueueOperation IOperation<EnqueueOperation>.Create(nuint generation)
            => new(generation);

        static ref nuint IOperation.GetPosition(ref State state) => ref state.Producer;

        public override string ToString() => generation.ToString("X");
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct DequeueOperation : IOperation<DequeueOperation>
    {
        private readonly nuint generation;

        private DequeueOperation(nuint generation) => this.generation = generation;

        static bool IOperation.Retry(ref readonly bool frozenForEnqueues) => true;

        static bool IOperation.CanRetry(ref readonly RingBuffer<T> state, nuint consumerPosition)
            => consumerPosition != state.Producer;

        // the slot can be occupied in the next generation only
        nuint IOperation.NextSequence => (generation + 1U) & ~StateBit;

        bool IOperation.IsValidSequence(nuint sequence) => sequence == (generation | StateBit);

        static DequeueOperation IOperation<DequeueOperation>.Create(nuint generation)
            => new(generation);
        
        static ref nuint IOperation.GetPosition(ref State state) => ref state.Consumer;

        public override string ToString() => generation.ToString("X");
    }
    
    private readonly nuint Producer => Volatile.Read(in state.Producer);

    private readonly nuint Consumer => Volatile.Read(in state.Consumer);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private readonly nuint ConsumerNextGen => Volatile.Read(in state.Consumer) + (uint)slots.Length;
    
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