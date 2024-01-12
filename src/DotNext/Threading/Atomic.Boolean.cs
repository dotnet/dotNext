using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

public static partial class Atomic
{
    /// <summary>
    /// Represents atomic boolean.
    /// </summary>
    /// <remarks>
    /// Initializes a new atomic boolean container with initial value.
    /// </remarks>
    /// <param name="value">Initial value of the atomic boolean.</param>
    [SuppressMessage("Usage", "CA2231")]
    public struct Boolean(bool value) : IEquatable<bool>
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Negation : ISupplier<bool, bool>
        {
            bool ISupplier<bool, bool>.Invoke(bool value) => !value;
        }

        private int value = Unsafe.BitCast<bool, byte>(value);

        /// <summary>
        /// Gets or sets boolean value in volatile manner.
        /// </summary>
        public bool Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get => Unsafe.BitCast<byte, bool>((byte)Volatile.Read(in value));

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => Volatile.Write(ref this.value, Unsafe.BitCast<bool, byte>(value));
        }

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="update">The new value.</param>
        /// <param name="expected">The expected value.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareExchange(bool update, bool expected)
            => Unsafe.BitCast<byte, bool>((byte)Interlocked.CompareExchange(ref value, Unsafe.BitCast<bool, byte>(update), Unsafe.BitCast<bool, byte>(expected)));

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompareAndSet(bool expected, bool update)
            => CompareExchange(update, expected) == expected;

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

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>Negation result.</returns>
        public unsafe bool NegateAndGet() => Update(new Negation()).NewValue;

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <returns>The original value before negation.</returns>
        public unsafe bool GetAndNegate() => Update(new Negation()).OldValue;

        /// <summary>
        /// Modifies the current value atomically.
        /// </summary>
        /// <param name="update">A new value to be stored into this container.</param>
        /// <returns>Original value before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool GetAndSet(bool update)
            => Unsafe.BitCast<byte, bool>((byte)Interlocked.Exchange(ref value, Unsafe.BitCast<bool, byte>(update)));

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

        private (bool OldValue, bool NewValue) Update<TUpdater>(TUpdater updater)
            where TUpdater : notnull, ISupplier<bool, bool>
        {
            bool oldValue, newValue, tmp = Value;
            do
            {
                newValue = updater.Invoke(oldValue = tmp);
            }
            while ((tmp = CompareExchange(newValue, oldValue)) != oldValue);

            return (oldValue, newValue);
        }

        private (bool OldValue, bool NewValue) Accumulate<TAccumulator>(bool x, TAccumulator accumulator)
            where TAccumulator : notnull, ISupplier<bool, bool, bool>
        {
            bool oldValue, newValue, tmp = Value;
            do
            {
                newValue = accumulator.Invoke(oldValue = tmp, x);
            }
            while ((tmp = CompareExchange(newValue, oldValue)) != oldValue);

            return (oldValue, newValue);
        }

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        public bool AccumulateAndGet<TAccumulator>(bool x, TAccumulator accumulator)
            where TAccumulator : notnull, ISupplier<bool, bool, bool>
            => Accumulate(x, accumulator).NewValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        public bool AccumulateAndGet(bool x, Func<bool, bool, bool> accumulator)
            => AccumulateAndGet<DelegatingSupplier<bool, bool, bool>>(x, accumulator);

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        public bool GetAndAccumulate<TAccumulator>(bool x, TAccumulator accumulator)
            where TAccumulator : notnull, ISupplier<bool, bool, bool>
            => Accumulate(x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        public bool GetAndAccumulate(bool x, Func<bool, bool, bool> accumulator)
            => GetAndAccumulate<DelegatingSupplier<bool, bool, bool>>(x, accumulator);

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public bool UpdateAndGet<TUpdater>(TUpdater updater)
            where TUpdater : notnull, ISupplier<bool, bool>
            => Update(updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public bool UpdateAndGet(Func<bool, bool> updater)
            => UpdateAndGet<DelegatingSupplier<bool, bool>>(updater);

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        public bool GetAndUpdate<TUpdater>(TUpdater updater)
            where TUpdater : notnull, ISupplier<bool, bool>
            => Update(updater).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        public bool GetAndUpdate(Func<bool, bool> updater)
            => GetAndUpdate<DelegatingSupplier<bool, bool>>(updater);

        internal void Acquire()
        {
            ref var lockState = ref value;
            if (Interlocked.Exchange(ref lockState, 1) is 1)
                Contention(ref lockState);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void Contention(ref int value)
            {
                var spinner = new SpinWait();
                do
                {
                    spinner.SpinOnce();
                }
                while (Interlocked.Exchange(ref value, 1) is 1);
            }
        }

        internal void Release() => Volatile.Write(ref value, 0);

        /// <summary>
        /// Determines whether stored value is equal to value passed as argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public readonly bool Equals(bool other) => Volatile.Read(in value) == Unsafe.BitCast<bool, byte>(other);

        /// <summary>
        /// Computes hash code for the stored value.
        /// </summary>
        /// <returns>The hash code of the stored boolean value.</returns>
        public override readonly int GetHashCode() => Volatile.Read(in value);

        /// <summary>
        /// Determines whether stored value is equal to
        /// value as the passed argument.
        /// </summary>
        /// <param name="other">Other value to compare.</param>
        /// <returns><see langword="true"/>, if stored value is equal to other value; otherwise, <see langword="false"/>.</returns>
        public override readonly bool Equals([NotNullWhen(true)] object? other) => other switch
        {
            bool b => Equals(b),
            Boolean b => Value == b.Value,
            _ => false,
        };

        /// <summary>
        /// Returns stored boolean value in the form of <see cref="string"/>.
        /// </summary>
        /// <returns>Textual representation of stored boolean value.</returns>
        public override readonly string ToString() => Value ? bool.TrueString : bool.FalseString;
    }
}