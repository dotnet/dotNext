using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    using Runtime.InteropServices;

    /// <summary>
    /// Represents atomic operations that can be applied to the value referenced by pointer.
    /// </summary>
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
        public static void VolatileWrite(this Pointer<long> pointer, long value) => AtomicInt64.VolatileWrite(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<int> pointer, int value) => AtomicInt32.VolatileWrite(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<short> pointer, short value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<byte> pointer, byte value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<bool> pointer, bool value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<IntPtr> pointer, IntPtr value) => AtomicIntPtr.VolatileWrite(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<float> pointer, float value) => AtomicSingle.VolatileWrite(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<uint> pointer, uint value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<ushort> pointer, ushort value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<sbyte> pointer, sbyte value) => Volatile.Write(ref pointer.Value, value);

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
        public static void VolatileWrite(this Pointer<UIntPtr> pointer, UIntPtr value) => Volatile.Write(ref pointer.Value, value);

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
        public static long VolatileRead(this Pointer<long> pointer) => AtomicInt64.VolatileRead(ref pointer.Value);

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
        public static int VolatileRead(this Pointer<int> pointer) => AtomicInt32.VolatileRead(ref pointer.Value);

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
        public static short VolatileRead(this Pointer<short> pointer) => Volatile.Read(ref pointer.Value);

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
        public static byte VolatileRead(this Pointer<byte> pointer) => Volatile.Read(ref pointer.Value);

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
        public static bool VolatileRead(this Pointer<bool> pointer) => Volatile.Read(ref pointer.Value);

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
        public static IntPtr VolatileRead(this Pointer<IntPtr> pointer) => AtomicIntPtr.VolatileRead(ref pointer.Value);

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
        public static float VolatileRead(this Pointer<float> pointer) => AtomicSingle.VolatileRead(ref pointer.Value);

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
        public static double VolatileRead(this Pointer<double> pointer) => AtomicDouble.VolatileRead(ref pointer.Value);

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
        public static uint VolatileRead(this Pointer<uint> pointer) => Volatile.Read(ref pointer.Value);

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
        public static ushort VolatileRead(this Pointer<ushort> pointer) => Volatile.Read(ref pointer.Value);

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
        public static sbyte VolatileRead(this Pointer<sbyte> pointer) => Volatile.Read(ref pointer.Value);

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
        public static UIntPtr VolatileRead(this Pointer<UIntPtr> pointer) => Volatile.Read(ref pointer.Value);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be incremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long IncrementValue(this Pointer<long> pointer) => AtomicInt64.IncrementAndGet(ref pointer.Value);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be incremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr IncrementValue(this Pointer<IntPtr> pointer) => AtomicIntPtr.IncrementAndGet(ref pointer.Value);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be incremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IncrementValue(this Pointer<int> pointer) => AtomicInt32.IncrementAndGet(ref pointer.Value);

        /// <summary>
        /// Increments a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be incremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float IncrementValue(this Pointer<float> pointer) => AtomicSingle.IncrementAndGet(ref pointer.Value);

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
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long DecrementValue(this Pointer<long> pointer) => AtomicInt64.DecrementAndGet(ref pointer.Value);

        /// <summary>
        /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be decremented.</param>
        /// <returns>The decremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr DecrementValue(this Pointer<IntPtr> pointer) => AtomicIntPtr.DecrementAndGet(ref pointer.Value);

        /// <summary>
        /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be decremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DecrementValue(this Pointer<int> pointer) => AtomicInt32.DecrementAndGet(ref pointer.Value);

        /// <summary>
        /// Decrements a value located in the memory at the address specified by pointer and stores the result, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be decremented.</param>
        /// <returns>The incremented value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DecrementValue(this Pointer<float> pointer) => AtomicSingle.DecrementAndGet(ref pointer.Value);

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
        public static long GetAndSetValue(this Pointer<long> pointer, long update) => AtomicInt64.GetAndSet(ref pointer.Value, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAndSetValue(this Pointer<int> pointer, int update) => AtomicInt32.GetAndSet(ref pointer.Value, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndSetValue(this Pointer<float> pointer, float update) => AtomicSingle.GetAndSet(ref pointer.Value, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value in the memory.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double GetAndSetValue(this Pointer<double> pointer, double update) => AtomicDouble.GetAndSet(ref pointer.Value, update);

        /// <summary>
        /// Sets a value located in the memory at the address specified by pointer to a specified value as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="update">The value to which the memory is set.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndSetValue(this Pointer<IntPtr> pointer, IntPtr update) => AtomicIntPtr.GetAndSet(ref pointer.Value, update);

        /// <summary>
        /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AddValue(this Pointer<int> pointer, int value) => AtomicInt32.Add(ref pointer.Value, value);

        /// <summary>
        /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr AddValue(this Pointer<IntPtr> pointer, IntPtr value) => AtomicIntPtr.Add(ref pointer.Value, value);

        /// <summary>
        /// Adds two integers and replaces the first integer with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value to be added to the integer located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AddValue(this Pointer<long> pointer, long value) => AtomicInt64.Add(ref pointer.Value, value);

        /// <summary>
        /// Adds two numbers and replaces the first number with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value to be added to the number located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float AddValue(this Pointer<float> pointer, float value) => AtomicSingle.Add(ref pointer.Value, value);

        /// <summary>
        /// Adds two numbers and replaces the first number with the sum, as an atomic operation.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value to be added to the number located in the memory at the address specified by pointer.</param>
        /// <returns>The new value stored at memory address.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double AddValue(this Pointer<double> pointer, double value) => AtomicDouble.Add(ref pointer.Value, value);

        /// <summary>
        /// Compares two 64-bit signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long CompareExchangeValue(this Pointer<long> pointer, long value, long comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

        /// <summary>
        /// Compares two native-sized signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr CompareExchangeValue(this Pointer<IntPtr> pointer, IntPtr value, IntPtr comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

        /// <summary>
        /// Compares two 32-bit signed integers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareExchangeValue(this Pointer<int> pointer, int value, int comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

        /// <summary>
        /// Compares two 32-bit floating-point numbers for equality and, if they are equal, replaces the first value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
        /// <param name="comparand">The value that is compared to the value at the memory address.</param>
        /// <returns>The original value that was in the memory before.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float CompareExchangeValue(this Pointer<float> pointer, float value, float comparand) => Interlocked.CompareExchange(ref pointer.Value, value, comparand);

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
        public static bool CompareAndSetValue(this Pointer<long> pointer, long expected, long update) => AtomicInt64.CompareAndSet(ref pointer.Value, expected, update);

        /// <summary>
        /// Atomically sets a value located at the specified address in the memory to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSetValue(this Pointer<IntPtr> pointer, IntPtr expected, IntPtr update) => AtomicIntPtr.CompareAndSet(ref pointer.Value, expected, update);

        /// <summary>
        /// Atomically sets a value located at the specified address in the memory to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSetValue(this Pointer<int> pointer, int expected, int update) => AtomicInt32.CompareAndSet(ref pointer.Value, expected, update);

        /// <summary>
        /// Atomically sets a value located at the specified address in the memory to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="expected">The expected value.</param>
        /// <param name="update">The new value.</param>
        /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool CompareAndSetValue(this Pointer<float> pointer, float expected, float update) => AtomicSingle.CompareAndSet(ref pointer.Value, expected, update);

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
        public static int AccumulateAndGetValue(this Pointer<int> pointer, int x, Func<int, int, int> accumulator) => AtomicInt32.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
        public static int AccumulateAndGetValue(this Pointer<int> pointer, int x, in ValueFunc<int, int, int> accumulator) => AtomicInt32.AccumulateAndGet(ref pointer.Value, x, in accumulator);

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
        public static int GetAndAccumulateValue(this Pointer<int> pointer, int x, Func<int, int, int> accumulator) => AtomicInt32.GetAndAccumulate(ref pointer.Value, x, accumulator);

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
        public static int GetAndAccumulateValue(this Pointer<int> pointer, int x, in ValueFunc<int, int, int> accumulator) => AtomicInt32.GetAndAccumulate(ref pointer.Value, x, in accumulator);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UpdateAndGetValue(this Pointer<int> pointer, Func<int, int> updater) => AtomicInt32.UpdateAndGet(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int UpdateAndGetValue(this Pointer<int> pointer, in ValueFunc<int, int> updater) => AtomicInt32.UpdateAndGet(ref pointer.Value, in updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAndUpdateValue(this Pointer<int> pointer, Func<int, int> updater) => AtomicInt32.GetAndUpdate(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetAndUpdateValue(this Pointer<int> pointer, in ValueFunc<int, int> updater) => AtomicInt32.GetAndUpdate(ref pointer.Value, in updater);

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
        public static long AccumulateAndGetValue(this Pointer<long> pointer, long x, Func<long, long, long> accumulator) => AtomicInt64.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
        public static long AccumulateAndGetValue(this Pointer<long> pointer, long x, in ValueFunc<long, long, long> accumulator) => AtomicInt64.AccumulateAndGet(ref pointer.Value, x, in accumulator);

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
        public static long GetAndAccumulateValue(this Pointer<long> pointer, long x, Func<long, long, long> accumulator) => AtomicInt64.GetAndAccumulate(ref pointer.Value, x, accumulator);

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
        public static long GetAndAccumulateValue(this Pointer<long> pointer, long x, in ValueFunc<long, long, long> accumulator) => AtomicInt64.GetAndAccumulate(ref pointer.Value, x, in accumulator);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UpdateAndGetValue(this Pointer<long> pointer, Func<long, long> updater) => AtomicInt64.UpdateAndGet(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UpdateAndGetValue(this Pointer<long> pointer, in ValueFunc<long, long> updater) => AtomicInt64.UpdateAndGet(ref pointer.Value, in updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetAndUpdateValue(this Pointer<long> pointer, Func<long, long> updater) => AtomicInt64.GetAndUpdate(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long GetAndUpdateValue(this Pointer<long> pointer, in ValueFunc<long, long> updater) => AtomicInt64.GetAndUpdate(ref pointer.Value, in updater);

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
        public static float AccumulateAndGetValue(this Pointer<float> pointer, float x, Func<float, float, float> accumulator) => AtomicSingle.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
        public static float AccumulateAndGetValue(this Pointer<float> pointer, float x, in ValueFunc<float, float, float> accumulator) => AtomicSingle.AccumulateAndGet(ref pointer.Value, x, in accumulator);

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
        public static float GetAndAccumulateValue(this Pointer<float> pointer, float x, Func<float, float, float> accumulator) => AtomicSingle.GetAndAccumulate(ref pointer.Value, x, accumulator);

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
        public static float GetAndAccumulateValue(this Pointer<float> pointer, float x, in ValueFunc<float, float, float> accumulator) => AtomicSingle.GetAndAccumulate(ref pointer.Value, x, in accumulator);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateAndGetValue(this Pointer<float> pointer, Func<float, float> updater) => AtomicSingle.UpdateAndGet(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float UpdateAndGetValue(this Pointer<float> pointer, in ValueFunc<float, float> updater) => AtomicSingle.UpdateAndGet(ref pointer.Value, in updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndUpdateValue(this Pointer<float> pointer, Func<float, float> updater) => AtomicSingle.GetAndUpdate(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAndUpdateValue(this Pointer<float> pointer, in ValueFunc<float, float> updater) => AtomicSingle.GetAndUpdate(ref pointer.Value, in updater);

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
        public static double AccumulateAndGetValue(this Pointer<double> pointer, double x, in ValueFunc<double, double, double> accumulator) => AtomicDouble.AccumulateAndGet(ref pointer.Value, x, in accumulator);

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
        public static double GetAndAccumulateValue(this Pointer<double> pointer, double x, in ValueFunc<double, double, double> accumulator) => AtomicDouble.GetAndAccumulate(ref pointer.Value, x, in accumulator);

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
        public static double UpdateAndGetValue(this Pointer<double> pointer, in ValueFunc<double, double> updater) => AtomicDouble.UpdateAndGet(ref pointer.Value, in updater);

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
        public static double GetAndUpdateValue(this Pointer<double> pointer, in ValueFunc<double, double> updater) => AtomicDouble.GetAndUpdate(ref pointer.Value, in updater);

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
        public static IntPtr AccumulateAndGetValue(this Pointer<IntPtr> pointer, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator) => AtomicIntPtr.AccumulateAndGet(ref pointer.Value, x, accumulator);

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
        public static IntPtr AccumulateAndGetValue(this Pointer<IntPtr> pointer, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator) => AtomicIntPtr.AccumulateAndGet(ref pointer.Value, x, in accumulator);

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
        public static IntPtr GetAndAccumulateValue(this Pointer<IntPtr> pointer, IntPtr x, Func<IntPtr, IntPtr, IntPtr> accumulator) => AtomicIntPtr.GetAndAccumulate(ref pointer.Value, x, accumulator);

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
        public static IntPtr GetAndAccumulateValue(this Pointer<IntPtr> pointer, IntPtr x, in ValueFunc<IntPtr, IntPtr, IntPtr> accumulator) => AtomicIntPtr.GetAndAccumulate(ref pointer.Value, x, in accumulator);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr UpdateAndGetValue(this Pointer<IntPtr> pointer, Func<IntPtr, IntPtr> updater) => AtomicIntPtr.UpdateAndGet(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr UpdateAndGetValue(this Pointer<IntPtr> pointer, in ValueFunc<IntPtr, IntPtr> updater) => AtomicIntPtr.UpdateAndGet(ref pointer.Value, in updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndUpdateValue(this Pointer<IntPtr> pointer, Func<IntPtr, IntPtr> updater) => AtomicIntPtr.GetAndUpdate(ref pointer.Value, updater);

        /// <summary>
        /// Atomically updates the value referenced by pointer with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="pointer">A pointer to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IntPtr GetAndUpdateValue(this Pointer<IntPtr> pointer, in ValueFunc<IntPtr, IntPtr> updater) => AtomicIntPtr.GetAndUpdate(ref pointer.Value, in updater);
    }
}