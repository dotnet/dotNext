using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Various atomic operations for <see cref="long"/> data type
/// accessible as extension methods.
/// </summary>
/// <remarks>
/// Methods exposed by this class provide volatile read/write
/// of the field even if it is not declared as volatile field.
/// </remarks>
/// <seealso cref="Interlocked"/>
public static class AtomicInt64
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
    public static long VolatileRead(in this long value) => Volatile.Read(ref Unsafe.AsRef(in value));

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
    public static void VolatileWrite(ref this long value, long newValue) => Volatile.Write(ref value, newValue);

    /// <summary>
    /// Atomically increments by one referenced value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long IncrementAndGet(ref this long value)
        => Interlocked.Increment(ref value);

    /// <summary>
    /// Atomically decrements by one the current value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long DecrementAndGet(ref this long value)
        => Interlocked.Decrement(ref value);

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet(ref this long value, long expected, long update)
        => Interlocked.CompareExchange(ref value, update, expected) == expected;

    /// <summary>
    /// Adds two 64-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long AddAndGet(ref this long value, long operand)
        => Interlocked.Add(ref value, operand);

    /// <summary>
    /// Adds two 64-bit integers and replaces referenced integer with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndAdd(ref this long value, long operand)
        => Accumulate(ref value, operand, new Adder()).OldValue;

    /// <summary>
    /// Bitwise "ands" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndBitwiseAnd(ref this long value, long operand)
        => Interlocked.And(ref value, operand);

    /// <summary>
    /// Bitwise "ands" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitwiseAndAndGet(ref this long value, long operand)
        => Interlocked.And(ref value, operand) & operand;

    /// <summary>
    /// Bitwise "ors" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndBitwiseOr(ref this long value, long operand)
        => Interlocked.Or(ref value, operand);

    /// <summary>
    /// Bitwise "ors" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitwiseOrAndGet(ref this long value, long operand)
        => Interlocked.Or(ref value, operand) | operand;

    /// <summary>
    /// Bitwise "xors" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The original value.</returns>

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndBitwiseXor(ref this long value, long operand)
        => Accumulate(ref value, operand, new BitwiseXor()).OldValue;

    /// <summary>
    /// Bitwise "xors" two 64-bit integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be cmobined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long BitwiseXorAndGet(ref this long value, long operand)
        => Accumulate(ref value, operand, new BitwiseXor()).NewValue;

    /// <summary>
    /// Modifies referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndSet(ref this long value, long update)
        => Interlocked.Exchange(ref value, update);

    /// <summary>
    /// Modifies value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long SetAndGet(ref this long value, long update)
    {
        VolatileWrite(ref value, update);
        return update;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (long OldValue, long NewValue) Update<TUpdater>(ref long value, TUpdater updater)
        where TUpdater : struct, ISupplier<long, long>
    {
        long oldValue, newValue;
        do
        {
            newValue = updater.Invoke(oldValue = VolatileRead(in value));
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (long OldValue, long NewValue) Accumulate<TAccumulator>(ref long value, long x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<long, long, long>
    {
        long oldValue, newValue;
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
    public static long AccumulateAndGet(ref this long value, long x, Func<long, long, long> accumulator)
        => Accumulate<DelegatingSupplier<long, long, long>>(ref value, x, accumulator).NewValue;

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
    public static unsafe long AccumulateAndGet(ref this long value, long x, delegate*<long, long, long> accumulator)
        => Accumulate<Supplier<long, long, long>>(ref value, x, accumulator).NewValue;

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
    public static long GetAndAccumulate(ref this long value, long x, Func<long, long, long> accumulator)
        => Accumulate<DelegatingSupplier<long, long, long>>(ref value, x, accumulator).OldValue;

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
    public static unsafe long GetAndAccumulate(ref this long value, long x, delegate*<long, long, long> accumulator)
        => Accumulate<Supplier<long, long, long>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long UpdateAndGet(ref this long value, Func<long, long> updater)
        => Update<DelegatingSupplier<long, long>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe long UpdateAndGet(ref this long value, delegate*<long, long> updater)
        => Update<Supplier<long, long>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetAndUpdate(ref this long value, Func<long, long> updater)
        => Update<DelegatingSupplier<long, long>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe long GetAndUpdate(ref this long value, delegate*<long, long> updater)
        => Update<Supplier<long, long>>(ref value, updater).OldValue;
}