using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents atomic enum value.
    /// </summary>
    [Serializable]
    [SuppressMessage("Design", "CA1066")]
    [SuppressMessage("Usage", "CA2231")]
    public struct AtomicEnum<E> : IEquatable<E>, ISerializable, IAtomicWrapper<long, E>
        where E : unmanaged, Enum
    {
        private const string ValueSerData = "value";

        private long value;

        /// <summary>
        /// Initializes a new atomic boolean container with initial value.
        /// </summary>
        /// <param name="value">Initial value of the atomic boolean.</param>
        public AtomicEnum(E value) => this.value = value.ToInt64();

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private AtomicEnum(SerializationInfo info, StreamingContext context)
        {
            value = (long)info.GetValue(ValueSerData, typeof(long));
        }

        E IAtomicWrapper<long, E>.Convert(long value) => value.ToEnum<E>();

        long IAtomicWrapper<long, E>.Convert(E value) => value.ToInt64();

        Atomic<long> IAtomicWrapper<long, E>.Atomic => AtomicInt64.Atomic;

        /// <summary>
        /// Gets or sets enum value in volatile manner.
        /// </summary>
        public E Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.VolatileRead().ToEnum<E>();
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.value.VolatileWrite(value.ToInt64());
        }

        /// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>The original value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E CompareExchange(E update, E expected)
            => Atomic<long, E, AtomicEnum<E>>.CompareExchange(ref this, ref value, update, expected);

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(E expected, E update) => Atomic<long, E, AtomicEnum<E>>.CompareAndSet(ref this, ref value, expected, update);

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>Original value before modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E GetAndSet(E update) => Atomic<long, E, AtomicEnum<E>>.GetAndSet(ref this, ref value, update);

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>A new value passed as argument.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public E SetAndGet(E update)
        {
            Value = update;
            return update;
        }

        /// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the updated value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The updated value.</returns>
		public E AccumulateAndGet(E x, Func<E, E, E> accumulator)
            => Atomic<long, E, AtomicEnum<E>>.Accumulate(ref this, ref value, x, accumulator).NewValue;

        /// <summary>
		/// Atomically updates the current value with the results of applying the given function 
		/// to the current and given values, returning the original value.
		/// </summary>
		/// <remarks>
		/// The function is applied with the current value as its first argument, and the given update as the second argument.
		/// </remarks>
		/// <param name="x">Accumulator operand.</param>
		/// <param name="accumulator">A side-effect-free function of two arguments</param>
		/// <returns>The original value.</returns>
		public E GetAndAccumulate(E x, Func<E, E, E> accumulator)
            => Atomic<long, E, AtomicEnum<E>>.Accumulate(ref this, ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public E UpdateAndGet(Func<E, E> updater)
            => Atomic<long, E, AtomicEnum<E>>.Update(ref this, ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public E GetAndUpdate(Func<E, E> updater)
            => Atomic<long, E, AtomicEnum<E>>.Update(ref this, ref value, updater).OldValue;

        /// <summary>
        /// Determines whether stored value is equal to
        /// value passed as argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(E other) => value.VolatileRead() == other.ToInt64();

        /// <summary>
        /// Determines whether stored value is equal to
        /// value as the passed argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other)
        {
            switch (other)
            {
                case E b:
                    return Equals(b);
                case AtomicEnum<E> b:
                    return b.value.VolatileRead() == value.VolatileRead();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override int GetHashCode() => value.VolatileRead().GetHashCode();

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => info.AddValue(ValueSerData, value);
    }
}