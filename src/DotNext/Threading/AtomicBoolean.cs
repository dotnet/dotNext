using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents atomic boolean.
    /// </summary>
    [Serializable]
    [SuppressMessage("Design", "CA1066")]
    [SuppressMessage("Usage", "CA2231")]
    public struct AtomicBoolean : IEquatable<bool>, ISerializable, IAtomicWrapper<int, bool>
    {
        private const string ValueSerData = "value";
        private const int True = 1;
        private const int False = 0;
        private int value;

        /// <summary>
        /// Initializes a new atomic boolean container with initial value.
        /// </summary>
        /// <param name="value">Initial value of the atomic boolean.</param>
        public AtomicBoolean(bool value) => this.value = value ? True : False;

        [SuppressMessage("Usage", "CA1801", Justification = "context is required by .NET serialization framework")]
        private AtomicBoolean(SerializationInfo info, StreamingContext context)
        {
            value = (int)info.GetValue(ValueSerData, typeof(int));
        }

        bool IAtomicWrapper<int, bool>.Convert(int value) => value == True;

        int IAtomicWrapper<int, bool>.Convert(bool value) => value ? True : False;

        Atomic<int> IAtomicWrapper<int, bool>.Atomic => AtomicInt32.Atomic;

        /// <summary>
        /// Gets or sets boolean value in volatile manner.
        /// </summary>
        public bool Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value.VolatileRead() == True;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this.value.VolatileWrite(value ? True : False);
        }

        /// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns>The original value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareExchange(bool update, bool expected)
            => Atomic<int, bool, AtomicBoolean>.CompareExchange(ref this, ref value, update, expected);

        /// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(bool expected, bool update)
            => Atomic<int, bool, AtomicBoolean>.CompareAndSet(ref this, ref value, expected, update);

        /// <summary>
        /// Atomically sets <see langword="true"/> value if the
        /// current value is <see langword="false"/>.
        /// </summary>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool FalseToTrue() => CompareAndSet(false, true);

        /// <summary>
        /// Atomically sets <see langword="false"/> value if the
        /// current value is <see langword="true"/>.
        /// </summary>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrueToFalse() => CompareAndSet(true, false);

        private static bool Negate(bool value) => !value;

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>Negation result.</returns>
        public bool NegateAndGet() => UpdateAndGet(Negate);

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>The original value before negation.</returns>
        public bool GetAndNegate() => GetAndUpdate(Negate);

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>Original value before modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetAndSet(bool update)
            => Atomic<int, bool, AtomicBoolean>.GetAndSet(ref this, ref value, update);

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>A new value passed as argument.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool SetAndGet(bool update)
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
		public bool AccumulateAndGet(bool x, Func<bool, bool, bool> accumulator)
            => Atomic<int, bool, AtomicBoolean>.Accumulate(ref this, ref value, x, accumulator).NewValue;

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
		public bool GetAndAccumulate(bool x, Func<bool, bool, bool> accumulator)
            => Atomic<int, bool, AtomicBoolean>.Accumulate(ref this, ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public bool UpdateAndGet(Func<bool, bool> updater)
            => Atomic<int, bool, AtomicBoolean>.Update(ref this, ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public bool GetAndUpdate(Func<bool, bool> updater)
            => Atomic<int, bool, AtomicBoolean>.Update(ref this, ref value, updater).OldValue;

        /// <summary>
        /// Determines whether stored value is equal to value passed as argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(bool other) => value.VolatileRead() == (other ? True : False);

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override int GetHashCode() => value.VolatileRead();

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
                case bool b:
                    return Equals(b);
                case AtomicBoolean b:
                    return b.value.VolatileRead() == value.VolatileRead();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Returns stored boolean value in the form of <see cref="string"/>.
        /// </summary>
        /// <returns>Textual representation of stored boolean value.</returns>
        public override string ToString() => value == True ? bool.TrueString : bool.FalseString;

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
            => info.AddValue(ValueSerData, value);
    }
}