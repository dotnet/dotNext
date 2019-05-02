using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents atomic boolean.
    /// </summary>
    [Serializable]
    public struct AtomicBoolean : IEquatable<bool>
    {
        private const int True = 1;
        private const int False = 0;
        private int value;

        /// <summary>
        /// Initializes a new atomic boolean container with initial value.
        /// </summary>
        /// <param name="value">Initial value of the atomic boolean.</param>
        public AtomicBoolean(bool value) => this.value = value ? True : False;

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
            => Interlocked.CompareExchange(ref value, update ? True : False, expected ? True : False) == True;

        /// <summary>
		/// Atomically sets referenced value to the given updated value if the current value == the expected value.
		/// </summary>
		/// <param name="expected">The expected value.</param>
		/// <param name="update">The new value.</param>
		/// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(bool expected, bool update)
            => value.CompareAndSet(expected ? True : False, update ? True : False);

        /// <summary>
        /// Atomically sets <see langword="true"/> value if the
        /// current value is <see langword="false"/>.
        /// </summary>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool FalseToTrue() => Interlocked.CompareExchange(ref value, True, False) == False;

        /// <summary>
        /// Atomically sets <see langword="false"/> value if the
        /// current value is <see langword="true"/>.
        /// </summary>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TrueToFalse() => Interlocked.CompareExchange(ref value, False, True) == True;

        private (int OldValue, int NewValue) Negate()
        {
            int oldValue, newValue;
            do
            {
                oldValue = value.VolatileRead();
                newValue = oldValue ^ True;
            }
            while (!value.CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>Negation result.</returns>
        public bool NegateAndGet() => Negate().NewValue == True;

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>The original value before negation.</returns>
        public bool GetAndNegate() => Negate().OldValue == True;

        /// <summary>
		/// Modifies the current value atomically.
		/// </summary>
		/// <param name="update">A new value to be stored into this container.</param>
		/// <returns>Original value before modification.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetAndSet(bool update)
            => Interlocked.Exchange(ref value, update ? True : False) == True;

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

        private (bool OldValue, bool NewValue) Accumulate(bool x, Func<bool, bool, bool> accumulator)
        {
            bool oldValue, newValue;
            do
            {
                newValue = accumulator(oldValue = Value, x);
            }
            while (!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
        }

        private (bool OldValue, bool NewValue) Update(Func<bool, bool> updater)
        {
            bool oldValue, newValue;
            do
            {
                newValue = updater(oldValue = Value);
            }
            while (!CompareAndSet(oldValue, newValue));
            return (oldValue, newValue);
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
            => Accumulate(x, accumulator).NewValue;

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
		public bool GetAndAcummulate(bool x, Func<bool, bool, bool> accumulator)
            => Accumulate(x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The updated value.</returns>
        public bool UpdateAndGet(Func<bool, bool> updater)
            => Update(updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results 
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function</param>
        /// <returns>The original value.</returns>
        public bool GetAndUpdate(Func<bool, bool> updater)
            => Update(updater).OldValue;

        /// <summary>
        /// Determines whether stored value is equal to
        /// value as the passed argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(bool other) => value == (other ? True : False);

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override int GetHashCode() => value;

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
    }
}