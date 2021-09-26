using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Various atomic operations for <see cref="uint"/> data type
/// accessible as extension methods.
/// </summary>
/// <remarks>
/// Methods exposed by this class provide volatile read/write
/// of the field even if it is not declared as volatile field.
/// </remarks>
/// <seealso cref="Interlocked"/>
[CLSCompliant(false)]
public static class AtomicUInt32
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
    public static uint VolatileRead(in this uint value) => Volatile.Read(ref Unsafe.AsRef(in value));

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
    public static void VolatileWrite(ref this uint value, uint newValue) => Volatile.Write(ref value, newValue);

    /// <summary>
    /// Atomically increments the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint IncrementAndGet(ref this uint value)
        => Interlocked.Increment(ref value);

    /// <summary>
    /// Atomically decrements the referenced value by one.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint DecrementAndGet(ref this uint value)
        => Interlocked.Decrement(ref value);

    /// <summary>
    /// Atomically sets the referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet(ref this uint value, uint expected, uint update)
        => Interlocked.CompareExchange(ref value, update, expected) == expected;

    /// <summary>
    /// Adds two 32-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Add(ref this uint value, uint operand)
        => Interlocked.Add(ref value, operand);

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndSet(ref this uint value, uint update)
        => Interlocked.Exchange(ref value, update);

    /// <summary>
    /// Modifies the referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SetAndGet(ref this uint value, uint update)
    {
        VolatileWrite(ref value, update);
        return update;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (uint OldValue, uint NewValue) Update<TUpdater>(ref uint value, TUpdater updater)
        where TUpdater : struct, ISupplier<uint, uint>
    {
        uint oldValue, newValue;
        do
        {
            newValue = updater.Invoke(oldValue = VolatileRead(in value));
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (uint OldValue, uint NewValue) Accumulate<TAccumulator>(ref uint value, uint x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<uint, uint, uint>
    {
        uint oldValue, newValue;
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
    public static uint AccumulateAndGet(ref this uint value, uint x, Func<uint, uint, uint> accumulator)
        => Accumulate<DelegatingSupplier<uint, uint, uint>>(ref value, x, accumulator).NewValue;

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
    public static unsafe uint AccumulateAndGet(ref this uint value, uint x, delegate*<uint, uint, uint> accumulator)
        => Accumulate<Supplier<uint, uint, uint>>(ref value, x, accumulator).NewValue;

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
    public static uint GetAndAccumulate(ref this uint value, uint x, Func<uint, uint, uint> accumulator)
        => Accumulate<DelegatingSupplier<uint, uint, uint>>(ref value, x, accumulator).OldValue;

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
    public static unsafe uint GetAndAccumulate(ref this uint value, uint x, delegate*<uint, uint, uint> accumulator)
        => Accumulate<Supplier<uint, uint, uint>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint UpdateAndGet(ref this uint value, Func<uint, uint> updater)
        => Update<DelegatingSupplier<uint, uint>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint UpdateAndGet(ref this uint value, delegate*<uint, uint> updater)
        => Update<Supplier<uint, uint>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndUpdate(ref this uint value, Func<uint, uint> updater)
        => Update<DelegatingSupplier<uint, uint>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe uint GetAndUpdate(ref this uint value, delegate*<uint, uint> updater)
        => Update<Supplier<uint, uint>>(ref value, updater).OldValue;

    /// <summary>
    /// Performs volatile read of the array element.
    /// </summary>
    /// <param name="array">The array to read from.</param>
    /// <param name="index">The array element index.</param>
    /// <returns>The array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint VolatileRead(this uint[] array, long index)
        => VolatileRead(in array[index]);

    /// <summary>
    /// Performs volatile write to the array element.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The array element index.</param>
    /// <param name="value">The new value of the array element.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite(this uint[] array, long index, uint value)
        => VolatileWrite(ref array[index], value);

    /// <summary>
    /// Atomically increments the array element by one.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The index of the element to increment atomically.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint IncrementAndGet(this uint[] array, long index)
        => IncrementAndGet(ref array[index]);

    /// <summary>
    /// Atomically decrements the array element by one.
    /// </summary>
    /// <param name="array">The array to write into.</param>
    /// <param name="index">The index of the array element to decrement atomically.</param>
    /// <returns>Decremented array element.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint DecrementAndGet(this uint[] array, long index)
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
    public static uint CompareExchange(this uint[] array, long index, uint update, uint comparand)
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
    public static bool CompareAndSet(this uint[] array, long index, uint expected, uint update)
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
    public static uint Add(this uint[] array, long index, uint operand)
        => Add(ref array[index], operand);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>Original array element before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint GetAndSet(this uint[] array, long index, uint update)
        => GetAndSet(ref array[index], update);

    /// <summary>
    /// Modifies the array element atomically.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of array element to be modified.</param>
    /// <param name="update">A new value to be stored as array element.</param>
    /// <returns>The array element after modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint SetAndGet(this uint[] array, long index, uint update)
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
    public static uint AccumulateAndGet(this uint[] array, long index, uint x, Func<uint, uint, uint> accumulator)
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
    public static unsafe uint AccumulateAndGet(this uint[] array, long index, uint x, delegate*<uint, uint, uint> accumulator)
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
    public static uint GetAndAccumulate(this uint[] array, long index, uint x, Func<uint, uint, uint> accumulator)
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
    public static unsafe uint GetAndAccumulate(this uint[] array, long index, uint x, delegate*<uint, uint, uint> accumulator)
        => GetAndAccumulate(ref array[index], x, accumulator);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public static uint UpdateAndGet(this uint[] array, long index, Func<uint, uint> updater)
        => UpdateAndGet(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    public static unsafe uint UpdateAndGet(this uint[] array, long index, delegate*<uint, uint> updater)
        => UpdateAndGet(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value of the array element.</returns>
    public static uint GetAndUpdate(this uint[] array, long index, Func<uint, uint> updater)
        => GetAndUpdate(ref array[index], updater);

    /// <summary>
    /// Atomically updates the array element with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="array">The array to be modified.</param>
    /// <param name="index">The index of the array element to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value of the array element.</returns>
    public static unsafe uint GetAndUpdate(this uint[] array, long index, delegate*<uint, uint> updater)
        => GetAndUpdate(ref array[index], updater);
}