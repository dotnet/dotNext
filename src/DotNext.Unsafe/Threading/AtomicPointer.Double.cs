using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using Runtime.InteropServices;

public static partial class AtomicPointer
{
    /// <summary>
    /// Writes a value to the memory location identified by the pointer .
    /// </summary>
    /// <remarks>
    /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows:
    /// If a read or write appears before this method in the code, the processor cannot move it after this method.
    /// </remarks>
    /// <param name="pointer">The pointer to write.</param>
    /// <param name="value">The value to write. The value is written immediately so that it is visible to all processors in the computer.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void VolatileWrite(this Pointer<double> pointer, double value) => AtomicDouble.VolatileWrite(ref pointer.Value, value);

    /// <summary>
    /// Reads the value from the memory location identified by the pointer.
    /// </summary>
    /// <remarks>
    /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows:
    /// If a read or write appears after this method in the code, the processor cannot move it before this method.
    /// </remarks>
    /// <param name="pointer">The pointer to read.</param>
    /// <returns>The value that was read. This value is the latest written by any processor in the computer, regardless of the number of processors or the state of processor cache.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double VolatileRead(this Pointer<double> pointer) => AtomicDouble.VolatileRead(in pointer.Value);

    /// <summary>
    /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be incremented.</param>
    /// <returns>The incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double IncrementValue(this Pointer<double> pointer) => AtomicDouble.IncrementAndGet(ref pointer.Value);

    /// <summary>
    /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be decremented.</param>
    /// <returns>The incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double DecrementValue(this Pointer<double> pointer) => AtomicDouble.DecrementAndGet(ref pointer.Value);

    /// <summary>
    /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="update">The value to which the memory is set.</param>
    /// <returns>The original value in the memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndSetValue(this Pointer<double> pointer, double update) => AtomicDouble.GetAndSet(ref pointer.Value, update);

    /// <summary>
    /// Adds two numbers and replaces the first number with the sum, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="value">The value to be added to the number located in the memory at the address specified by pointer.</param>
    /// <returns>The new value stored at memory address.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AddAndGetValue(this Pointer<double> pointer, double value) => AtomicDouble.AddAndGet(ref pointer.Value, value);

    /// <summary>
    /// Adds two numbers and replaces the first number with the sum, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="value">The value to be added to the number located in the memory at the address specified by pointer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndAddValue(this Pointer<double> pointer, double value) => AtomicDouble.GetAndAdd(ref pointer.Value, value);

    /// <summary>
    /// Compares two 64-bit floating-point numbers for equality and, if they are equal, replaces the first value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at the memory address.</param>
    /// <returns>The original value that was in the memory before.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CompareExchangeValue(this Pointer<double> pointer, double value, double comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

    /// <summary>
    /// Atomically sets a value located at the specified address in the memory to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool CompareAndSetValue(this Pointer<double> pointer, double expected, double update) => AtomicDouble.CompareAndSet(ref pointer.Value, expected, update);

    /// <summary>
    /// Atomically updates the current value referenced by pointer with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double AccumulateAndGetValue(this Pointer<double> pointer, double x, Func<double, double, double> accumulator) => AtomicDouble.AccumulateAndGet(ref pointer.Value, x, accumulator);

    /// <summary>
    /// Atomically updates the current value referenced by pointer with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double AccumulateAndGetValue(this Pointer<double> pointer, double x, delegate*<double, double, double> accumulator) => AtomicDouble.AccumulateAndGet(ref pointer.Value, x, accumulator);

    /// <summary>
    /// Atomically updates the current value referenced by pointer with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndAccumulateValue(this Pointer<double> pointer, double x, Func<double, double, double> accumulator) => AtomicDouble.GetAndAccumulate(ref pointer.Value, x, accumulator);

    /// <summary>
    /// Atomically updates the current value referenced by pointer with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double GetAndAccumulateValue(this Pointer<double> pointer, double x, delegate*<double, double, double> accumulator) => AtomicDouble.GetAndAccumulate(ref pointer.Value, x, accumulator);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double UpdateAndGetValue(this Pointer<double> pointer, Func<double, double> updater) => AtomicDouble.UpdateAndGet(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double UpdateAndGetValue(this Pointer<double> pointer, delegate*<double, double> updater) => AtomicDouble.UpdateAndGet(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double GetAndUpdateValue(this Pointer<double> pointer, Func<double, double> updater) => AtomicDouble.GetAndUpdate(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe double GetAndUpdateValue(this Pointer<double> pointer, delegate*<double, double> updater) => AtomicDouble.GetAndUpdate(ref pointer.Value, updater);
}