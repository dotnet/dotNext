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
    [CLSCompliant(false)]
    public static void VolatileWrite(this Pointer<ulong> pointer, ulong value) => Volatile.Write(ref pointer.Value, value);

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
    [CLSCompliant(false)]
    public static ulong VolatileRead(this Pointer<ulong> pointer) => Volatile.Read(ref pointer.Value);

    /// <summary>
    /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be incremented.</param>
    /// <returns>The incremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong IncrementValue(this Pointer<ulong> pointer) => AtomicUInt64.IncrementAndGet(ref pointer.Value);

    /// <summary>
    /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be decremented.</param>
    /// <returns>The decremented value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong DecrementValue(this Pointer<ulong> pointer) => AtomicUInt64.DecrementAndGet(ref pointer.Value);

    /// <summary>
    /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="update">The value to which the memory is set.</param>
    /// <returns>The original value in the memory.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndSetValue(this Pointer<ulong> pointer, ulong update) => AtomicUInt64.GetAndSet(ref pointer.Value, update);

    /// <summary>
    /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
    /// <returns>The new value stored at memory address.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong AddAndGetValue(this Pointer<ulong> pointer, ulong value) => AtomicUInt64.AddAndGet(ref pointer.Value, value);

    /// <summary>
    /// Compares two 64-bit signed integers for equality and, if they are equal, replaces the first value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
    /// <param name="comparand">The value that is compared to the value at the memory address.</param>
    /// <returns>The original value that was in the memory before.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong CompareExchangeValue(this Pointer<ulong> pointer, ulong value, ulong comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

    /// <summary>
    /// Atomically sets a value located at the specified address in the memory to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static bool CompareAndSetValue(this Pointer<ulong> pointer, ulong expected, ulong update) => AtomicUInt64.CompareAndSet(ref pointer.Value, expected, update);

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
    public static ulong AccumulateAndGetValue(this Pointer<ulong> pointer, ulong x, Func<ulong, ulong, ulong> accumulator) => AtomicUInt64.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
    public static unsafe ulong AccumulateAndGetValue(this Pointer<ulong> pointer, ulong x, delegate*<ulong, ulong, ulong> accumulator) => AtomicUInt64.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
    public static ulong GetAndAccumulateValue(this Pointer<ulong> pointer, ulong x, Func<ulong, ulong, ulong> accumulator) => AtomicUInt64.GetAndAccumulate(ref pointer.Value, x, accumulator);

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
    public static unsafe ulong GetAndAccumulateValue(this Pointer<ulong> pointer, ulong x, delegate*<ulong, ulong, ulong> accumulator) => AtomicUInt64.GetAndAccumulate(ref pointer.Value, x, accumulator);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong UpdateAndGetValue(this Pointer<ulong> pointer, Func<ulong, ulong> updater) => AtomicUInt64.UpdateAndGet(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The updated value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe ulong UpdateAndGetValue(this Pointer<ulong> pointer, delegate*<ulong, ulong> updater) => AtomicUInt64.UpdateAndGet(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndUpdateValue(this Pointer<ulong> pointer, Func<ulong, ulong> updater) => AtomicUInt64.GetAndUpdate(ref pointer.Value, updater);

    /// <summary>
    /// Atomically updates the value referenced by pointer with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="updater">A side-effect-free function.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe ulong GetAndUpdateValue(this Pointer<ulong> pointer, delegate*<ulong, ulong> updater) => AtomicUInt64.GetAndUpdate(ref pointer.Value, updater);

    /// <summary>
    /// Bitwise "ands" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndBitwiseAndGetValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.GetAndBitwiseAnd(ref pointer.Value, operand);

    /// <summary>
    /// Bitwise "ands" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong BitwiseAndAndGetValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.BitwiseAndAndGet(ref pointer.Value, operand);

    /// <summary>
    /// Bitwise "ors" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndBitwiseOrValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.GetAndBitwiseOr(ref pointer.Value, operand);

    /// <summary>
    /// Bitwise "ors" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong BitwiseOrAndGetValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.BitwiseOrAndGet(ref pointer.Value, operand);

    /// <summary>
    /// Bitwise "xors" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The original value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong GetAndBitwiseXorValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.GetAndBitwiseXor(ref pointer.Value, operand);

    /// <summary>
    /// Bitwise "xors" two integers and replaces referenced integer with the result,
    /// as an atomic operation.
    /// </summary>
    /// <param name="pointer">A pointer to a value to be modified.</param>
    /// <param name="operand">The value to be combined with the currently stored integer.</param>
    /// <returns>The modified value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static ulong BitwiseXorAndGetValue(this Pointer<ulong> pointer, ulong operand) => AtomicUInt64.BitwiseXorAndGet(ref pointer.Value, operand);
}