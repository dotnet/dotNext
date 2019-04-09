using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    using Threading;
    using Runtime.InteropServices;

    public static class AtomicPointer
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
        public static void VolatileWrite(this Pointer<long> pointer, long value) => AtomicInt64.VolatileSet(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<int> pointer, int value) => AtomicInt32.VolatileSet(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<short> pointer, short value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<byte> pointer, byte value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<bool> pointer, bool value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<IntPtr> pointer, IntPtr value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<float> pointer, float value) => AtomicSingle.VolatileSet(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<double> pointer, double value) => AtomicDouble.VolatileSet(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<ulong> pointer, ulong value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<uint> pointer, uint value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<ushort> pointer, ushort value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<sbyte> pointer, sbyte value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<UIntPtr> pointer, UIntPtr value) => Volatile.Write(ref pointer.Ref, value);

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
        public static long VolatileRead(this Pointer<long> pointer) => AtomicInt64.VolatileGet(ref pointer.Ref);

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
        public static int VolatileRead(this Pointer<int> pointer) => AtomicInt32.VolatileGet(ref pointer.Ref);

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
        public static short VolatileRead(this Pointer<short> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static byte VolatileRead(this Pointer<byte> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static bool VolatileRead(this Pointer<bool> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static IntPtr VolatileRead(this Pointer<IntPtr> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static float VolatileRead(this Pointer<float> pointer) => AtomicSingle.VolatileGet(ref pointer.Ref);

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
        public static double VolatileRead(this Pointer<double> pointer) => AtomicDouble.VolatileGet(ref pointer.Ref);

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
        public static ulong VolatileRead(this Pointer<ulong> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static uint VolatileRead(this Pointer<uint> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static ushort VolatileRead(this Pointer<ushort> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static sbyte VolatileRead(this Pointer<sbyte> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static UIntPtr VolatileRead(this Pointer<UIntPtr> pointer) => Volatile.Read(ref pointer.Ref);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long IncrementValue(this Pointer<long> pointer) => AtomicInt64.IncrementAndGet(ref pointer.Ref);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IncrementValue(this Pointer<int> pointer) => AtomicInt32.IncrementAndGet(ref pointer.Ref);

        /// <summary>
        /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long DecrementValue(this Pointer<long> pointer) => AtomicInt64.DecrementAndGet(ref pointer.Ref);

        /// <summary>
        /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DecrementValue(this Pointer<int> pointer) => AtomicInt32.DecrementAndGet(ref pointer.Ref);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetAndSetValue(this Pointer<long> pointer, long update) => AtomicInt64.GetAndSet(ref pointer.Ref, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAndSetValue(this Pointer<int> pointer, int update) => AtomicInt32.GetAndSet(ref pointer.Ref, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndSetValue(this Pointer<float> pointer, float update) => AtomicSingle.GetAndSet(ref pointer.Ref, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetAndSetValue(this Pointer<double> pointer, double update) => AtomicDouble.GetAndSet(ref pointer.Ref, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndSetValue(this Pointer<IntPtr> pointer, IntPtr update) => Interlocked.Exchange(ref pointer.Ref, update);

        /// <summary>
        /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddValue(this Pointer<int> pointer, int value) => AtomicInt32.Add(ref pointer.Ref, value);

        /// <summary>
        /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AddValue(this Pointer<long> pointer, long value) => AtomicInt64.Add(ref pointer.Ref, value);

        /// <summary>
        /// Compares two 64-bit signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CompareExchangeValue(this Pointer<long> pointer, long value, long comparand) => Interlocked.CompareExchange(ref pointer.Ref, value, comparand);

        /// <summary>
        /// Compares two 64-bit signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareExchangeValue(this Pointer<int> pointer, int value, int comparand) => Interlocked.CompareExchange(ref pointer.Ref, value, comparand);
    
        /// <summary>
        /// Compares two 64-bit signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">The pointer to the memory.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CompareExchangeValue(this Pointer<float> pointer, float value, float comparand) => Interlocked.CompareExchange(ref pointer.Ref, value, comparand);
    }
}