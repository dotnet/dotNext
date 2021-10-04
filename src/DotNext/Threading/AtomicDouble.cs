using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Various atomic operations for <see cref="double"/> data type
/// accessible as extension methods.
/// </summary>
/// <remarks>
/// Methods exposed by this class provide volatile read/write
/// of the field even if it is not declared as volatile field.
/// </remarks>
/// <seealso cref="Interlocked"/>
public static class AtomicDouble
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
    public static double VolatileRead(in this double value) => Volatile.Read(ref Unsafe.AsRef(in value));

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
    public static void VolatileWrite(ref this double value, double newValue) => Volatile.Write(ref value, newValue);

    /// <summary>
    /// Atomically increments by one referenced value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IncrementAndGet(ref this double value)
        => Update(ref value, new Increment()).NewValue;

    /// <summary>
    /// Atomically decrements by one the current value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <returns>Decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DecrementAndGet(ref this double value)
        => Update(ref value, new Decrement()).NewValue;

    /// <summary>
    /// Adds two 64-bit floating-point numbers and replaces referenced storage with the sum,
    /// as an atomic operation.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="operand">The value to be added to the currently stored integer.</param>
    /// <returns>Result of sum operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Add(ref this double value, double operand)
        => Accumulate(ref value, operand, new Adder()).NewValue;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Equals(double x, double y)
        => x == y || double.IsNaN(x) && double.IsNaN(y);

    /// <summary>
    /// Atomically sets referenced value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSet(ref this double value, double expected, double update)
        => Equals(Interlocked.CompareExchange(ref value, update, expected), expected);

    /// <summary>
    /// Modifies referenced value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>Original value before modification.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndSet(ref this double value, double update)
        => Interlocked.Exchange(ref value, update);

    /// <summary>
    /// Modifies value atomically.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="update">A new value to be stored into managed pointer.</param>
    /// <returns>A new value passed as argument.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double SetAndGet(ref this double value, double update)
    {
        VolatileWrite(ref value, update);
        return update;
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (double OldValue, double NewValue) Update<TUpdater>(ref double value, TUpdater updater)
        where TUpdater : struct, ISupplier<double, double>
    {
        double oldValue, newValue;
        do
        {
            newValue = updater.Invoke(oldValue = VolatileRead(in value));
        }
        while (!CompareAndSet(ref value, oldValue, newValue));
        return (oldValue, newValue);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static (double OldValue, double NewValue) Accumulate<TAccumulator>(ref double value, double x, TAccumulator accumulator)
        where TAccumulator : struct, ISupplier<double, double, double>
    {
        double oldValue, newValue;
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
    public static double AccumulateAndGet(ref this double value, double x, Func<double, double, double> accumulator)
        => Accumulate<DelegatingSupplier<double, double, double>>(ref value, x, accumulator).NewValue;

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
    public static unsafe double AccumulateAndGet(ref this double value, double x, delegate*<double, double, double> accumulator)
        => Accumulate<Supplier<double, double, double>>(ref value, x, accumulator).NewValue;

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
    public static double GetAndAccumulate(ref this double value, double x, Func<double, double, double> accumulator)
        => Accumulate<DelegatingSupplier<double, double, double>>(ref value, x, accumulator).OldValue;

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
    public static unsafe double GetAndAccumulate(ref this double value, double x, delegate*<double, double, double> accumulator)
        => Accumulate<Supplier<double, double, double>>(ref value, x, accumulator).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double UpdateAndGet(ref this double value, Func<double, double> updater)
        => Update<DelegatingSupplier<double, double>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double UpdateAndGet(ref this double value, delegate*<double, double> updater)
        => Update<Supplier<double, double>>(ref value, updater).NewValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndUpdate(ref this double value, Func<double, double> updater)
        => Update<DelegatingSupplier<double, double>>(ref value, updater).OldValue;

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="value">Reference to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double GetAndUpdate(ref this double value, delegate*<double, double> updater)
        => Update<Supplier<double, double>>(ref value, updater).OldValue;
}