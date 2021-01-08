using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Various atomic operations for <see cref="IntPtr"/> data type
    /// accessible as extension methods.
    /// </summary>
    /// <remarks>
    /// Methods exposed by this class provide volatile read/write
    /// of the field even if it is not declared as volatile field.
    /// </remarks>
    /// <seealso cref="Interlocked"/>
    public static class AtomicIntPtr
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
        public static IntPtr VolatileRead(in this IntPtr value)
            => Volatile.Read(ref Unsafe.AsRef(in value));

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
        public static void VolatileWrite(ref this IntPtr value, IntPtr newValue)
            => Volatile.Write(ref value, newValue);

        /// <summary>
        /// Atomically increments the referenced value by one.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <returns>Incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr IncrementAndGet(ref this IntPtr value)
        {
            return UpdateAndGet(ref value, new ValueFunc<nint, nint>(&Increment));

            static nint Increment(nint value) => value + 1;
        }

        /// <summary>
        /// Atomically decrements the referenced value by one.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <returns>Decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr DecrementAndGet(ref this IntPtr value)
        {
            return UpdateAndGet(ref value, new ValueFunc<nint, nint>(&Decrement));

            static nint Decrement(nint value) => value - 1;
        }

        /// <summary>
        /// Adds two native integers and replaces referenced storage with the sum,
        /// as an atomic operation.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="operand">The value to be added to the currently stored integer.</param>
        /// <returns>Result of sum operation.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe IntPtr Add(ref this IntPtr value, IntPtr operand)
        {
            return AccumulateAndGet(ref value, operand, new ValueFunc<nint, nint, nint>(&Add));

            static nint Add(nint x, nint y) => x + y;
        }

        /// <summary>
        /// Atomically sets the referenced value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSet(ref this IntPtr value, IntPtr expected, IntPtr update)
            => Interlocked.CompareExchange(ref value, update, expected) == expected;

        /// <summary>
        /// Modifies the referenced value atomically.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="update">A new value to be stored into managed pointer.</param>
        /// <returns>Original value before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndSet(ref this IntPtr value, IntPtr update)
            => Interlocked.Exchange(ref value, update);

        /// <summary>
        /// Modifies the referenced value atomically.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="update">A new value to be stored into managed pointer.</param>
        /// <returns>A new value passed as argument.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr SetAndGet(ref this IntPtr value, IntPtr update)
        {
            VolatileWrite(ref value, update);
            return update;
        }

        private static (IntPtr OldValue, IntPtr NewValue) Update(ref IntPtr value, in ValueFunc<IntPtr, IntPtr> updater)
        {
            IntPtr oldValue, newValue;
            do
            {
                newValue = updater.Invoke(oldValue = VolatileRead(in value));
            }
            while (!CompareAndSet(ref value, oldValue, newValue));
            return (oldValue, newValue);
        }

        private static (IntPtr OldValue, IntPtr NewValue) Accumulate(ref IntPtr value, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator)
        {
            IntPtr oldValue, newValue;
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
        public static IntPtr AccumulateAndGet(ref this IntPtr value, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator)
            => AccumulateAndGet(ref value, x, new ValueFunc<IntPtr, IntPtr, IntPtr>(accumulator));

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
        public static IntPtr AccumulateAndGet(ref this IntPtr value, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator)
            => Accumulate(ref value, x, accumulator).NewValue;

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
        public static IntPtr GetAndAccumulate(ref this IntPtr value, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator)
            => GetAndAccumulate(ref value, x, new ValueFunc<IntPtr, IntPtr, IntPtr>(accumulator));

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
        public static IntPtr GetAndAccumulate(ref this IntPtr value, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator)
            => Accumulate(ref value, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr UpdateAndGet(ref this IntPtr value, Func<IntPtr, IntPtr> updater)
            => UpdateAndGet(ref value, new ValueFunc<IntPtr, IntPtr>(updater));

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr UpdateAndGet(ref this IntPtr value, in ValueFunc<IntPtr, IntPtr> updater)
            => Update(ref value, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndUpdate(ref this IntPtr value, Func<IntPtr, IntPtr> updater)
            => GetAndUpdate(ref value, new ValueFunc<IntPtr, IntPtr>(updater));

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="value">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndUpdate(ref this IntPtr value, in ValueFunc<IntPtr, IntPtr> updater)
            => Update(ref value, updater).OldValue;

        /// <summary>
        /// Performs volatile read of the array element.
        /// </summary>
        /// <param name="array">The array to read from.</param>
        /// <param name="index">The array element index.</param>
        /// <returns>The array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr VolatileRead(this IntPtr[] array, long index)
            => VolatileRead(in array[index]);

        /// <summary>
        /// Performs volatile write to the array element.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The array element index.</param>
        /// <param name="value">The new value of the array element.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void VolatileWrite(this IntPtr[] array, long index, IntPtr value)
            => VolatileWrite(ref array[index], value);

        /// <summary>
        /// Atomically increments the array element by one.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The index of the element to increment atomically.</param>
        /// <returns>Incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr IncrementAndGet(this IntPtr[] array, long index)
            => IncrementAndGet(ref array[index]);

        /// <summary>
        /// Atomically decrements the array element by one.
        /// </summary>
        /// <param name="array">The array to write into.</param>
        /// <param name="index">The index of the array element to decrement atomically.</param>
        /// <returns>Decremented array element.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr DecrementAndGet(this IntPtr[] array, long index)
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
        public static IntPtr CompareExchange(this IntPtr[] array, long index, IntPtr update, IntPtr comparand)
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
        public static bool CompareAndSet(this IntPtr[] array, long index, IntPtr expected, IntPtr update)
            => CompareAndSet(ref array[index], expected, update);

        /// <summary>
        /// Modifies the array element atomically.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
        /// <param name="update">A new value to be stored as array element.</param>
        /// <returns>Original array element before modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndSet(this IntPtr[] array, long index, IntPtr update)
            => GetAndSet(ref array[index], update);

        /// <summary>
        /// Modifies the array element atomically.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of array element to be modified.</param>
        /// <param name="update">A new value to be stored as array element.</param>
        /// <returns>The array element after modification.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr SetAndGet(this IntPtr[] array, long index, IntPtr update)
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
        public static IntPtr AccumulateAndGet(this IntPtr[] array, long index, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator)
            => AccumulateAndGet(array, index, x, new ValueFunc<IntPtr, IntPtr, IntPtr>(accumulator));

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
        public static IntPtr AccumulateAndGet(this IntPtr[] array, long index, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator)
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
        public static IntPtr GetAndAccumulate(this IntPtr[] array, long index, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator)
            => GetAndAccumulate(array, index, x, new ValueFunc<IntPtr, IntPtr, IntPtr>(accumulator));

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
        public static IntPtr GetAndAccumulate(this IntPtr[] array, long index, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator)
            => GetAndAccumulate(ref array[index], x, accumulator);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public static IntPtr UpdateAndGet(this IntPtr[] array, long index, Func<IntPtr, IntPtr> updater)
            => UpdateAndGet(array, index, new ValueFunc<IntPtr, IntPtr>(updater));

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        public static IntPtr UpdateAndGet(this IntPtr[] array, long index, in ValueFunc<IntPtr, IntPtr> updater)
            => UpdateAndGet(ref array[index], updater);

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value of the array element.</returns>
        public static IntPtr GetAndUpdate(this IntPtr[] array, long index, Func<IntPtr, IntPtr> updater)
            => GetAndUpdate(array, index, new ValueFunc<IntPtr, IntPtr>(updater));

        /// <summary>
        /// Atomically updates the array element with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="array">The array to be modified.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value of the array element.</returns>
        public static IntPtr GetAndUpdate(this IntPtr[] array, long index, in ValueFunc<IntPtr, IntPtr> updater)
            => GetAndUpdate(ref array[index], updater);
    }
}
