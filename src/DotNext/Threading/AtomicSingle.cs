using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Various atomic operations for <see cref="float"/> data type
    /// accessible as extension methods.
    /// </summary>
    /// <remarks>
    /// Methods exposed by this class provide volatile read/write
    /// of the field even if it is not declared as volatile field.
    /// </remarks>
    /// <seealso cref="Interlocked"/>
    public static class AtomicSingle
    {
        /// <summary>
        /// Reads the value of the specified field. On systems that require it, inserts a
        /// memory barrier that prevents the processor from reordering memory operations
        /// as follows: If a read or write appears after this method in the code, the processor
        /// cannot move it before this method.
        /// </summary>
        /// <param name="value">The field to read.</param>
        /// <returns>
        /// The value that was read. This value is the latest written by any processor in
        /// the computer, regardless of the number of processors or the state of processor
        /// cache.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VolatileRead(in this float value) => Volatile.Read(ref Unsafe.AsRef(in value));

        /// <summary>
        /// Writes the specified value to the specified field. On systems that require it,
        /// inserts a memory barrier that prevents the processor from reordering memory operations
        /// as follows: If a read or write appears before this method in the code, the processor
        /// cannot move it after this method.
        /// </summary>
        /// <param name="value">The field where the value is written.</param>
        /// <param name="newValue">
        /// The value to write. The value is written immediately so that it is visible to
        /// all processors in the computer.
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite(ref this float value, float newValue) => Volatile.Write(ref value, newValue);

        /// <summary>
        /// Atomically increments by one referenced value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <returns>Incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float IncrementAndGet(ref this float value)
            => Update(ref value, new Increment()).NewValue;

        /// <summary>
        /// Atomically decrements by one the current value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <returns>Decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecrementAndGet(ref this float value)
            => Update(ref value, new Decrement()).NewValue;

        /// <summary>
        /// Adds two 64-bit floating-point numbers and replaces referenced storage with the sum,
        /// as an atomic operation.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="operand">The value to be added to the currently stored integer.</param>
        /// <returns>Result of sum operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Add(ref this float value, float operand)
            => Accumulate(ref value, operand, new Adder()).NewValue;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Equals(float x, float y)
            => x == y || float.IsNaN(x) && float.IsNaN(y);

        /// <summary>
        /// Atomically sets referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSet(ref this float value, float expected, float update)
            => Equals(Interlocked.CompareExchange(ref value, update, expected), expected);

        /// <summary>
        /// Modifies referenced value atomically.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="update">A new value to be stored into managed pointer.</param>
        /// <returns>Original value before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndSet(ref this float value, float update)
            => Interlocked.Exchange(ref value, update);

        /// <summary>
        /// Modifies value atomically.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="update">A new value to be stored into managed pointer.</param>
        /// <returns>A new value passed as argument.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SetAndGet(ref this float value, float update)
        {
            VolatileWrite(ref value, update);
            return update;
        }

#if !NETSTANDARD2_1
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static (float OldValue, float NewValue) Update<TUpdater>(ref float value, TUpdater updater)
            where TUpdater : struct, ISupplier<float, float>
        {
            float oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = VolatileRead(in value));
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

#if !NETSTANDARD2_1
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static (float OldValue, float NewValue) Accumulate<TAccumulator>(ref float value, float x, TAccumulator accumulator)
            where TAccumulator : struct, ISupplier<float, float, float>
        {
            float oldValue, newValue;
            do
            {
                newValue = accumulator.Invoke(oldValue = VolatileRead(in value), x);
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AccumulateAndGet(ref this float value, float x, Func<float, float, float> accumulator)
            => Accumulate<DelegatingSupplier<float, float, float>>(ref value, x, accumulator).NewValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe float AccumulateAndGet(ref this float value, float x, delegate*<float, float, float> accumulator)
            => Accumulate<Supplier<float, float, float>>(ref value, x, accumulator).NewValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndAccumulate(ref this float value, float x, Func<float, float, float> accumulator)
            => Accumulate<DelegatingSupplier<float, float, float>>(ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe float GetAndAccumulate(ref this float value, float x, delegate*<float, float, float> accumulator)
            => Accumulate<Supplier<float, float, float>>(ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateAndGet(ref this float value, Func<float, float> updater)
            => Update<DelegatingSupplier<float, float>>(ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe float UpdateAndGet(ref this float value, delegate*<float, float> updater)
            => Update<Supplier<float, float>>(ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndUpdate(ref this float value, Func<float, float> updater)
            => Update<DelegatingSupplier<float, float>>(ref value, updater).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe float GetAndUpdate(ref this float value, delegate*<float, float> updater)
            => Update<Supplier<float, float>>(ref value, updater).OldValue;

        /// <summary>
        /// Performs volatile read of the array element.
        /// </summary>
        /// <param name="array">The array to read from.</param>
        /// <param name="index">The array element index.</param>
        /// <returns>The array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float VolatileRead(this float[] array, long index)
            => VolatileRead(in array[index]);

        /// <summary>
        /// Performs volatile write to the array element.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The array element index.</param>
        /// <param name="value">The new value of the array element.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite(this float[] array, long index, float value)
            => VolatileWrite(ref array[index], value);

        /// <summary>
        /// Atomically increments the array element by one.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The index of the element to increment atomically.</param>
        /// <returns>Incremented array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float IncrementAndGet(this float[] array, long index)
            => IncrementAndGet(ref array[index]);

        /// <summary>
        /// Atomically decrements the array element by one.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The index of the array element to decrement atomically.</param>
        /// <returns>Decremented array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecrementAndGet(this float[] array, long index)
            => DecrementAndGet(ref array[index]);

        /// <summary>
        /// Atomically sets array element to the given updated value if the array element == the expected value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="update">The new value.</param>
        /// <param name="comparand">The expected value.</param>
        /// <returns>The original value of the array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CompareExchange(this float[] array, long index, float update, float comparand)
            => Interlocked.CompareExchange(ref array[index], update, comparand);

        /// <summary>
        /// Atomically sets array element to the given updated value if the array element == the expected value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSet(this float[] array, long index, float expected, float update)
            => CompareAndSet(ref array[index], expected, update);

        /// <summary>
        /// Adds two 64-bit integers and replaces array element with the sum,
        /// as an atomic operation.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="operand">The value to be added to the currently stored integer.</param>
        /// <returns>Result of sum operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Add(this float[] array, long index, float operand)
            => Add(ref array[index], operand);

        /// <summary>
        /// Modifies the array element atomically.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
        /// <param name="update">A new value to be stored as array element.</param>
        /// <returns>Original array element before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndSet(this float[] array, long index, float update)
            => GetAndSet(ref array[index], update);

        /// <summary>
        /// Modifies the array element atomically.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
        /// <param name="update">A new value to be stored as array element.</param>
        /// <returns>The array element after modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SetAndGet(this float[] array, long index, float update)
        {
            VolatileWrite(array, index, update);
            return update;
        }

        /// <summary>
        /// Atomically updates the array element with the results of applying the given function
        /// to the array element and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the array element as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        public static float AccumulateAndGet(this float[] array, long index, float x, Func<float, float, float> accumulator)
            => AccumulateAndGet(ref array[index], x, accumulator);

        /// <summary>
        /// Atomically updates the array element with the results of applying the given function
        /// to the array element and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the array element as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        [CLSCompliant(false)]
        public static unsafe float AccumulateAndGet(this float[] array, long index, float x, delegate*<float, float, float> accumulator)
            => AccumulateAndGet(ref array[index], x, accumulator);

        /// <summary>
        /// Atomically updates the array element with the results of applying the given function
        /// to the array element and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the array element as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value of the array element.</returns>
        public static float GetAndAccumulate(this float[] array, long index, float x, Func<float, float, float> accumulator)
            => GetAndAccumulate(ref array[index], x, accumulator);

        /// <summary>
        /// Atomically updates the array element with the results of applying the given function
        /// to the array element and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the array element as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value of the array element.</returns>
        [CLSCompliant(false)]
        public static unsafe float GetAndAccumulate(this float[] array, long index, float x, delegate*<float, float, float> accumulator)
            => GetAndAccumulate(ref array[index], x, accumulator);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public static float UpdateAndGet(this float[] array, long index, Func<float, float> updater)
            => UpdateAndGet(ref array[index], updater);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [CLSCompliant(false)]
        public static unsafe float UpdateAndGet(this float[] array, long index, delegate*<float, float> updater)
            => UpdateAndGet(ref array[index], updater);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value of the array element.</returns>
        public static float GetAndUpdate(this float[] array, long index, Func<float, float> updater)
            => GetAndUpdate(ref array[index], updater);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value of the array element.</returns>
        [CLSCompliant(false)]
        public static unsafe float GetAndUpdate(this float[] array, long index, delegate*<float, float> updater)
            => GetAndUpdate(ref array[index], updater);
    }
}
