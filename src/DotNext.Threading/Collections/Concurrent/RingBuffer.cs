#if !NETSTANDARD2_1
using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent
{
    using Threading;

    /// <summary>
    /// Represents concurrent non-blocking synchronous collection of fixed size based on ring buffer.
    /// </summary>
    /// <remarks>
    /// All synchronous methods do not allocate memory in the heap.
    /// The buffer implements FIFO ordering.
    /// </remarks>
    /// <typeparam name="T">The type of the elements in the collection.</typeparam>
    public partial class RingBuffer<T>
    {
        [StructLayout(LayoutKind.Auto)]
        internal struct Slot
        {
            internal T Value;
            private AtomicBoolean publishedFlag;

            internal void Acquire() => publishedFlag.Value = true;

            internal bool TryRelease() => publishedFlag.TrueToFalse();
        }

        /// <summary>
        /// Represents reserved slot in the buffer
        /// suitable for writing.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public ref struct Reservation
        {
            // TODO: replace with by-ref field when it will be added to C# and CLR
            private Span<Slot> slot;
            private Action? publishedCallback;

            internal Reservation(ref Slot slot, Action? publishedCallback)
            {
                this.slot = MemoryMarshal.CreateSpan(ref slot, 1);
                this.publishedCallback = publishedCallback;
            }

            private readonly ref Slot Slot => ref MemoryMarshal.GetReference(slot);

            /// <summary>
            /// Gets the value associated with the collection item.
            /// </summary>
            /// <exception cref="InvalidOperationException">The slot is already published.</exception>
            public readonly ref T Value
            {
                get
                {
                    ref var slot = ref Slot;

                    if (Unsafe.IsNullRef(ref slot))
                        throw new InvalidOperationException(ExceptionMessages.SlotAlreadyPublished);

                    return ref slot.Value;
                }
            }

            /// <summary>
            /// Advances the write cursor.
            /// </summary>
            /// <remarks>
            /// This method must be called once during the lifetime of this reservation.
            /// Otherwise, <see cref="InvalidOperationException"/> will be thrown.
            /// </remarks>
            /// <exception cref="InvalidOperationException">The slot is already published.</exception>
            public void Publish()
            {
                ref var slot = ref Slot;

                if (Unsafe.IsNullRef(ref slot))
                    throw new InvalidOperationException(ExceptionMessages.SlotAlreadyPublished);

                slot.Acquire();
                publishedCallback?.Invoke();
                this = default;
            }

            /// <summary>
            /// Places the value into the reserved slot and mark it as published.
            /// </summary>
            /// <remarks>
            /// This method must be called once during the lifetime of this reservation.
            /// Otherwise, <see cref="InvalidOperationException"/> will be thrown.
            /// </remarks>
            /// <param name="value">The value to be placed into the buffer.</param>
            public void SetAndPublish(T value)
            {
                ref var slot = ref Slot;

                if (Unsafe.IsNullRef(ref slot))
                    throw new InvalidOperationException(ExceptionMessages.SlotAlreadyPublished);

                slot.Value = value;
                slot.Acquire();
                publishedCallback?.Invoke();
                this = default;
            }
        }

        /// <summary>
        /// Represents acquired slot from the buffer
        /// suitable for reading.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public ref struct Acquisition
        {
            private Span<long> cursor;
            private Span<T> value;
            private Action? consumedCallback;

            internal Acquisition(ref long readCursor, ref Slot slot, Action? consumedCallback)
            {
                cursor = MemoryMarshal.CreateSpan(ref readCursor, 1);
                value = MemoryMarshal.CreateSpan(ref slot.Value, 1);
                this.consumedCallback = consumedCallback;
            }

            /// <summary>
            /// Advances the read cursor.
            /// </summary>
            /// <remarks>
            /// This method must be called once during the lifetime of this acquisition.
            /// Otherwise, <see cref="InvalidOperationException"/> will be thrown.
            /// </remarks>
            /// <exception cref="InvalidOperationException">The slot is already consumed.</exception>
            public void Consume()
            {
                ref var readCusor = ref MemoryMarshal.GetReference(cursor);

                if (Unsafe.IsNullRef(ref readCusor))
                    throw new InvalidOperationException(ExceptionMessages.SlotAlreadyConsumed);

                readCusor.IncrementAndGet();
                consumedCallback?.Invoke();
                this = default;
            }

            /// <summary>
            /// Returns the copy of the value and mark it as consumed.
            /// </summary>
            /// <remarks>
            /// This method must be called once during the lifetime of this acquisition.
            /// Otherwise, <see cref="InvalidOperationException"/> will be thrown.
            /// </remarks>
            /// <returns>The consumed value.</returns>
            /// <exception cref="InvalidOperationException">The slot is already consumed.</exception>
            public T GetAndConsume()
            {
                ref var readCursor = ref MemoryMarshal.GetReference(cursor);

                if (Unsafe.IsNullRef(ref readCursor))
                    throw new InvalidOperationException(ExceptionMessages.SlotAlreadyConsumed);

                T result;

                if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                {
                    ref var holder = ref MemoryMarshal.GetReference(value);
                    result = holder;
                    holder = default!;
                }
                else
                {
                    result = Value;
                }

                readCursor.IncrementAndGet();
                consumedCallback?.Invoke();
                this = default;
                return result;
            }

            /// <summary>
            /// Gets the value associated with the buffer slot.
            /// </summary>
            /// <exception cref="InvalidOperationException">The slot is already consumed.</exception>
            public readonly ref T Value
            {
                get
                {
                    ref var result = ref MemoryMarshal.GetReference(value);

                    if (Unsafe.IsNullRef(ref result))
                        throw new InvalidOperationException(ExceptionMessages.SlotAlreadyConsumed);

                    return ref result;
                }
            }
        }

        private readonly Slot[] buffer;
        private readonly long indexMask;

        private Action? publishedCallback, consumedCallback;
        private long produced; // producer cursor
        private long consumed; // consumer cursor

        /// <summary>
        /// Initializes a new ring buffer.
        /// </summary>
        /// <remarks>
        /// The actual capacity of the buffer is always a power of 2.
        /// If the supplied capacity doesn't follow this rule then constructor
        /// will adjust the value to the nearest correct value.
        /// </remarks>
        /// <param name="capacity">The capacity of the buffer.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is less than 2 or equal to <see cref="int.MaxValue"/>.</exception>
        public RingBuffer(int capacity)
        {
            capacity += 1; // we wasting the slot in the buffer to differentiate "full" and "empty" states

            if (capacity < 2 || capacity == int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            // ensure that capacity is the power of two
            if (BitOperations.PopCount(unchecked((uint)capacity)) != 1)
            {
                capacity = 1 << (32 - BitOperations.LeadingZeroCount(unchecked((ulong)capacity)));
            }

            indexMask = capacity - 1;
            buffer = new Slot[capacity];
        }

        /// <summary>
        /// Gets the capacity of this buffer.
        /// </summary>
        public int Capacity => buffer.Length - 1;

        /// <summary>
        /// Represents an event that fires when new items are added.
        /// </summary>
        public event Action Published
        {
            add => publishedCallback += value;
            remove => publishedCallback -= value;
        }

        /// <summary>
        /// Represents an event that fires when the item is consumed.
        /// </summary>
        public event Action Consumed
        {
            add => consumedCallback += value;
            remove => consumedCallback -= value;
        }

        internal TTarget? FindHandler<TTarget>()
            where TTarget : class
        {
            foreach (Action handler in publishedCallback?.GetInvocationList() ?? Array.Empty<Action>())
            {
                if (handler.Target is TTarget obj)
                    return obj;
            }

            foreach (Action handler in consumedCallback?.GetInvocationList() ?? Array.Empty<Action>())
            {
                if (handler.Target is TTarget obj)
                    return obj;
            }

            return null;
        }

        // the capacity is the power of 2 and mask bits are set to 1 so the bitwise and allows
        // to achive the same effect as by modulo operand and avoid integer overflow issues
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private long GetIndex(long value) => value & indexMask;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref Slot GetSlot(long offset)
            => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(buffer), (nint)GetIndex(offset));

        /// <summary>
        /// Allocates a new slot in the ring buffer.
        /// </summary>
        /// <remarks>
        /// This method allows to reserve the slot in the buffer without publishing a new value.
        /// <see cref="Reservation"/> allows you to initialize the object in the slot in thread-safe manner
        /// and then publish the modifications. The modifications must be published even in case of exception
        /// so the ensure that <see cref="Reservation.Publish"/> is called in any circumstances.
        /// Additionally, you can use <see cref="TryAdd(T)"/> as a high-level and safe alternative to this low-level
        /// method.
        /// </remarks>
        /// <param name="reservation">The reserved slot in the buffer.</param>
        /// <returns><see langword="true"/> if there is a free space available in the buffer; otherwise, <see langword="false"/>.</returns>
        public bool TryAllocate(out Reservation reservation)
        {
            long current, next;

            do
            {
                current = produced.VolatileRead();
                next = current + 1;

                // the next index is not yet consumed
                if (GetIndex(next) == GetIndex(consumed.VolatileRead()))
                {
                    reservation = default;
                    return false;
                }
            }
            while (!produced.CompareAndSet(current, next));

            reservation = new(ref GetSlot(current), publishedCallback);
            return true;
        }

        /// <summary>
        /// Attempts to add a new value to the buffer.
        /// </summary>
        /// <param name="value">The value to add.</param>
        /// <returns><see langword="true"/> if there is a free space available in the buffer for the value; otherwise, <see langword="false"/>.</returns>
        public bool TryAdd(T value)
        {
            if (!TryAllocate(out var reservation))
                return false;

            reservation.SetAndPublish(value);
            return true;
        }

        /// <summary>
        /// Attempts to acquire pupblished value in the buffer.
        /// </summary>
        /// <remarks>
        /// The value become readable only when it was published with <see cref="Reservation.Publish"/> method
        /// or added using <see cref="TryAdd(T)"/>. The acquired value is not marked as consumed until
        /// the call to <see cref="Acquisition.Consume"/>. You can access acquired object via <see cref="Acquisition.Value"/>
        /// in thead-safe manner but then you need to confirm consumption using <see cref="Acquisition.Consume"/> method.
        /// Additionally, you can use <see cref="TryRemove(out T)"/> method as high-level and safe alternative to this
        /// low-level method.
        /// </remarks>
        /// <param name="cursor"></param>
        /// <returns><see langword="true"/> if the value is acquired successfully; <see langword="false"/> if there is no available element in the buffer.</returns>
        public bool TryGet(out Acquisition cursor)
        {
            for (var current = consumed.VolatileRead(); ;)
            {
                ref var slot = ref GetSlot(current);
                if (slot.TryRelease())
                {
                    cursor = new(ref consumed, ref slot, consumedCallback);
                    return true;
                }

                var next = consumed.VolatileRead();

                // if read cursor wasn't changed during execution of this method
                // then leave; otherwise, we can try to consume again in the loop
                if (current == next)
                {
                    break;
                }
                else
                {
                    current = next;
                }
            }

            cursor = default;
            return false;
        }

        /// <summary>
        /// Attempts to remove previously added value.
        /// </summary>
        /// <param name="value">The value from the buffer.</param>
        /// <returns><see langword="true"/> if there is a value to consume; otherwise, <see langword="false"/>.</returns>
        public bool TryRemove([MaybeNullWhen(false)] out T value)
        {
            if (TryGet(out var acquisition))
            {
                value = acquisition.GetAndConsume();
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to remove previously added value.
        /// </summary>
        /// <returns>The value obtained from the buffer; or <see cref="Optional{T}.None"/> if there is no value to consume.</returns>
        public Optional<T> TryRemove()
            => TryGet(out var acquisition) ? acquisition.GetAndConsume() : Optional<T>.None;
    }
}
#endif