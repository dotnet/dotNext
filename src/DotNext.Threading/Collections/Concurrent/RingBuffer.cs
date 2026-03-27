using System.Diagnostics;
using System.Diagnostics.Contracts;
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
    private bool frozenForEnqueues;
    private RingBuffer state;

    public RingBuffer(int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        var length = nuint.CreateChecked(BitOperations.RoundUpToPowerOf2((ulong)(uint)maximumRetained));
        slots = new Slot[length];
        state = new(length);
        frozenForEnqueues = false;
    }

    public readonly bool IsFrozen => Volatile.Read(in frozenForEnqueues);

    public bool Freeze()
    {
        var frozen = Interlocked.FalseToTrue(ref frozenForEnqueues);
        if (frozen)
            WaitForPendingEnqueues();

        return frozen;
    }

    private void WaitForPendingEnqueues()
    {
        Debug.Assert(frozenForEnqueues);

        // the slots prior to the frozen position can be still in progress by the enqueuer, so wait for it
        for (var position = FreezeProducer() - 1U;
             this[state.GetIndex(position)].WaitForPendingEnqueue(state.GetGeneration(position));
             position--) ;
    }

    private nuint FreezeProducer()
    {
        Debug.Assert(frozenForEnqueues);
        
        const nuint shift = 2U;
        var current = state.Positions.Producer;
        for (nuint tmp, offset = (uint)slots.Length * shift;; current = tmp)
        {
            // Advances the producer too far forward, so this position cannot be reached naturally even
            // if the buffer is full. 2 generations forward is enough, because the items in the buffer
            // can be in the current and the next generation.
            tmp = Interlocked.CompareExchange(ref state.Positions.Producer, current + offset, current);
            if (tmp == current)
                break;
        }

        return current;
    }

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
        => ref DoOperation<RingBuffer.DequeueOperation>(ref state.Positions.Consumer, out sequence);

    public readonly bool IsEmpty
    {
        get
        {
            var position = state.Positions.Consumer;
            var generation = state.GetGeneration(position);

            return this[state.GetIndex(position)].Sequence != (generation | RingBuffer.StateBit);
        }
    }

    public ref Slot TryEnqueue(out nuint sequence)
        => ref DoOperation<RingBuffer.EnqueueOperation>(ref state.Positions.Producer, out sequence);

    private readonly ref Slot DoOperation<TOperation>(scoped ref nuint position, out nuint newSeq)
        where TOperation : struct, RingBuffer.IOperation<TOperation>, allows ref struct
    {
        var spinner = new SpinWait();
        for (nuint positionCopy = Volatile.Read(in position), tmp; TOperation.Retry(in frozenForEnqueues); positionCopy = tmp)
        {
            ref var slot = ref this[state.GetIndex(positionCopy)];
            var context = TOperation.Create(in state, positionCopy);

            if (!context.IsValidSequence(slot.Sequence))
            {
                if (!context.CanRetry(in state))
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

            return sequence == (frozenGen | RingBuffer.StateBit);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct RingBuffer(nuint length)
{
    private readonly nuint indexMask = length - 1U;
    private readonly int indexBits = int.CreateChecked(nuint.Log2(length));
    public State Positions;

    [Pure]
    public readonly nuint GetGeneration(nuint position) => position >>> indexBits;

    [Pure]
    public readonly nuint GetIndex(nuint position) => position & indexMask;
    
    public static nuint StateBit => (nuint)nint.MinValue;

    public interface IOperation<out TSelf>
        where TSelf : struct, IOperation<TSelf>, allows ref struct
    {
        static abstract bool Retry(ref readonly bool frozenForEnqueues);

        bool CanRetry(scoped ref readonly RingBuffer state);

        nuint NextSequence { get; }

        bool IsValidSequence(nuint sequence);

        public static abstract TSelf Create(scoped ref readonly RingBuffer state, nuint position);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct EnqueueOperation : IOperation<EnqueueOperation>
    {
        private readonly nuint generation, position;

        private EnqueueOperation(scoped ref readonly RingBuffer state, nuint producerPosition)
            => generation = state.GetGeneration(position = producerPosition);

        bool IOperation<EnqueueOperation>.CanRetry(ref readonly RingBuffer state)
        {
            var consumerPos = Volatile.Read(in state.Positions.Consumer);

            // the consumer must be in the same generation as the producer, or producer position must be less than the consumer position
            return state.GetGeneration(consumerPos) == generation || state.GetIndex(position) < state.GetIndex(consumerPos);
        }

        static bool IOperation<EnqueueOperation>.Retry(ref readonly bool frozenForEnqueues) => !Volatile.Read(in frozenForEnqueues);

        // the slot becomes available for consumption in the current generation
        nuint IOperation<EnqueueOperation>.NextSequence => generation | StateBit;

        bool IOperation<EnqueueOperation>.IsValidSequence(nuint sequence) => sequence == generation;

        static EnqueueOperation IOperation<EnqueueOperation>.Create(scoped ref readonly RingBuffer state, nuint producerPosition)
            => new(in state, producerPosition);

        public override string ToString() => generation.ToString("X");
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly ref struct DequeueOperation : IOperation<DequeueOperation>
    {
        private readonly nuint generation, position;

        private DequeueOperation(scoped ref readonly RingBuffer state, nuint consumerPosition)
            => generation = state.GetGeneration(position = consumerPosition);

        static bool IOperation<DequeueOperation>.Retry(ref readonly bool frozenForEnqueues) => true;

        bool IOperation<DequeueOperation>.CanRetry(ref readonly RingBuffer state)
            => position != Volatile.Read(in state.Positions.Producer);

        // the slot can be occupied in the next generation only
        nuint IOperation<DequeueOperation>.NextSequence => (generation + 1U) & ~StateBit;

        bool IOperation<DequeueOperation>.IsValidSequence(nuint sequence) => sequence == (generation | StateBit);

        static DequeueOperation IOperation<DequeueOperation>.Create(scoped ref readonly RingBuffer state, nuint consumerPosition)
            => new(in state, consumerPosition);

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