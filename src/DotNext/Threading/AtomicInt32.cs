using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Various atomic operations for <see cref="int"/> data type
/// accessible as extension methods.
/// </summary>
/// <remarks>
/// Methods exposed by this class provide volatile read/write
/// of the field even if it is not declared as volatile field.
/// </remarks>
/// <seealso cref="Interlocked"/>
public static class AtomicInt32
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
    public static int VolatileRead(in this int value) => Volatile.Read(ref Unsafe.AsRef(in value));

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
    public static void VolatileWrite(ref this int value, int newValue) => Volatile.Write(ref value, newValue);

    /// <summary>
    /// Atomically increments the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IncrementAndGet(ref this int value)
        => Interlocked.Increment(ref value);

    /// <summary>
    /// Atomically decrements the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecrementAndGet(ref this int value)
        => Interlocked.Decrement(ref value);

    /// <summary>
    /// Atomically sets the referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet(ref this int value, int expected, int update)
        => Interlocked.CompareExchange(ref value, update, expected) == expected;

    /// <summary>
    /// Adds two 32-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Add(ref this int value, int operand)
        => Interlocked.Add(ref value, operand);

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAndSet(ref this int value, int update)
        => Interlocked.Exchange(ref value, update);

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SetAndGet(ref this int value, int update)
    {
        VolatileWrite(ref value, update);
        return update;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (int OldValue, int NewValue) Update<TUpdater>(ref int value, TUpdater updater)
        where TUpdater : struct, ISupplier<int, int>
    {
        int oldValue, newValue;
        do
        {
            newValue = updater.Invoke(oldValue = VolatileRead(in value));
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (int OldValue, int NewValue) Accumulate<TAccumulator>(ref int value, int x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<int, int, int>
    {
        int oldValue, newValue;
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
    public static int AccumulateAndGet(ref this int value, int x, Func<int, int, int> accumulator)
        => Accumulate<DelegatingSupplier<int, int, int>>(ref value, x, accumulator).NewValue;

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
    public static unsafe int AccumulateAndGet(ref this int value, int x, delegate*<int, int, int> accumulator)
        => Accumulate<Supplier<int, int, int>>(ref value, x, accumulator).NewValue;

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
    public static int GetAndAccumulate(ref this int value, int x, Func<int, int, int> accumulator)
        => Accumulate<DelegatingSupplier<int, int, int>>(ref value, x, accumulator).OldValue;

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
    public static unsafe int GetAndAccumulate(ref this int value, int x, delegate*<int, int, int> accumulator)
        => Accumulate<Supplier<int, int, int>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int UpdateAndGet(ref this int value, Func<int, int> updater)
        => Update<DelegatingSupplier<int, int>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe int UpdateAndGet(ref this int value, delegate*<int, int> updater)
        => Update<Supplier<int, int>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAndUpdate(ref this int value, Func<int, int> updater)
        => Update<DelegatingSupplier<int, int>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe int GetAndUpdate(ref this int value, delegate*<int, int> updater)
        => Update<Supplier<int, int>>(ref value, updater).OldValue;

    /// <summary>
    /// Performs volatile read of the array element.
    /// </summary>
    /// <param name="array">The array to read from.</param>
    /// <param name="index">The array element index.</param>
    /// <returns>The array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int VolatileRead(this int[] array, long index)
        => VolatileRead(in array[index]);

    /// <summary>
    /// Performs volatile write to the array element.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The array element index.</param>
    /// <param name="value">The new value of the array element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite(this int[] array, long index, int value)
        => VolatileWrite(ref array[index], value);

    /// <summary>
    /// Atomically increments the array element by one.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The index of the element to increment atomically.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IncrementAndGet(this int[] array, long index)
        => IncrementAndGet(ref array[index]);

    /// <summary>
    /// Atomically decrements the array element by one.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The index of the array element to decrement atomically.</param>
    /// <returns>Decremented array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int DecrementAndGet(this int[] array, long index)
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
    public static int CompareExchange(this int[] array, long index, int update, int comparand)
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
    public static bool CompareAndSet(this int[] array, long index, int expected, int update)
        => CompareAndSet(ref array[index], expected, update);

    /// <summary>
    /// Adds two 32-bit integers and replaces array element with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Add(this int[] array, long index, int operand)
        => Add(ref array[index], operand);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>Original array element before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetAndSet(this int[] array, long index, int update)
        => GetAndSet(ref array[index], update);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>The array element after modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int SetAndGet(this int[] array, long index, int update)
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
    public static int AccumulateAndGet(this int[] array, long index, int x, Func<int, int, int> accumulator)
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
    public static unsafe int AccumulateAndGet(this int[] array, long index, int x, delegate*<int, int, int> accumulator)
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
    public static int GetAndAccumulate(this int[] array, long index, int x, Func<int, int, int> accumulator)
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
    public static unsafe int GetAndAccumulate(this int[] array, long index, int x, delegate*<int, int, int> accumulator)
        => GetAndAccumulate(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public static int UpdateAndGet(this int[] array, long index, Func<int, int> updater)
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
    public static unsafe int UpdateAndGet(this int[] array, long index, delegate*<int, int> updater)
        => UpdateAndGet(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value of the array element.</returns>
    public static int GetAndUpdate(this int[] array, long index, Func<int, int> updater)
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
    public static unsafe int GetAndUpdate(this int[] array, long index, delegate*<int, int> updater)
        => GetAndUpdate(ref array[index], updater);
}