using System;
using System.Threading.Tasks;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// Represents extension methods common to all unmanaged memory structures.
    /// </summary>
    public unsafe static class UnmanagedMemoryExtensions
    {
        /// <summary>
        /// Creates a copy of unmanaged memory inside of managed heap.
        /// </summary>
        /// <returns>A copy of unmanaged memory in the form of byte array.</returns>
        public static byte[] ToByteArray<M>(this ref M memory) where M : struct, IUnmanagedMemory => memory.ToPointer<byte>().ToByteArray(memory.Size);

        /// <summary>
		/// Represents unmanaged memory as stream.
		/// </summary>
        /// <param name="memory">The unmanaged memory.</param>
		/// <returns>A stream to unmanaged memory.</returns>
        public static UnmanagedMemoryStream AsStream<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().AsStream(memory.Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        public static void WriteTo<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteTo(destination, source.Size);

        /// <summary>
        /// Copies bytes from the memory location to the stream asynchronously.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination stream.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        public static Task WriteToAsync<M>(this ref M source, Stream destination)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteToAsync(destination, source.Size);

        /// <summary>
        /// Copies bytes from the memory location to the managed array of bytes.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination array.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this ref M source, byte[] destination, long offset, long count)
            where M : struct, IUnmanagedMemory
            => source.ToPointer<byte>().WriteTo(destination, offset, count);

        /// <summary>
        /// Copies bytes from the memory location to the managed array of bytes.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source memory location.</param>
        /// <param name="destination">The destination array.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this ref M source, byte[] destination)
            where M : struct, IUnmanagedMemory
            => source.WriteTo(destination, 0, destination.LongLength.UpperBounded(source.Size));

        /// <summary>
		/// Sets all bits of allocated memory to zero.
		/// </summary>
        /// <param name="memory">The unmanaged memory to be cleared.</param>
		/// <exception cref="NullPointerException">The memory is not allocated.</exception>
        public static void Clear<M>(this ref M memory)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().Clear(memory.Size);

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of arrays elements to be copied.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this byte[] source, M destination, long offset, long count)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFrom(source, offset, count);

        /// <summary>
        /// Copies bytes from the the managed array of bytes to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source array.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number of copied bytes.</returns>
        public static long WriteTo<M>(this byte[] source, M destination)
            where M : IUnmanagedMemory
            => source.WriteTo(destination, 0, source.LongLength.UpperBounded(destination.Size));

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static long WriteTo<M>(this Stream source, M destination)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFrom(source, destination.Size);

        /// <summary>
        /// Copies bytes from the stream to the given memory location.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="source">The source stream.</param>
        /// <param name="destination">The destination memory location.</param>
        /// <returns>The actual number</returns>
        public static Task<long> WriteToAsync<M>(this Stream source, M destination)
            where M : IUnmanagedMemory
            => destination.ToPointer<byte>().ReadFromAsync(source, destination.Size);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <typeparam name="M1">The first type of the unmanaged memory view.</typeparam>
        /// <typeparam name="M2">The second type of the unmanaged memory view.</typeparam>
        /// <param name="first">The first block of memory to be compared.</param>
        /// <param name="second">The second block of memory to be compared.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public static bool BitwiseEquals<M1, M2>(this ref M1 first, M2 second)
            where M1 : struct, IUnmanagedMemory
            where M2 : struct, IUnmanagedMemory
            => first.Size == second.Size && first.ToPointer<byte>().BitwiseEquals(second.ToPointer<byte>(), first.Size);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory.
        /// </summary>
        /// <typeparam name="M">The type of the unmanaged memory view.</typeparam>
        /// <param name="memory">The memory block.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public static int BitwiseHashCode<M>(this ref M memory, bool salted = true)
            where M : struct, IUnmanagedMemory
            => memory.ToPointer<byte>().BitwiseHashCode(memory.Size, salted);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <typeparam name="M1">The first type of the unmanaged memory view.</typeparam>
        /// <typeparam name="M2">The second type of the unmanaged memory view.</typeparam>
        /// <param name="first">The first block of memory to be compared.</param>
        /// <param name="second">The second block of memory to be compared.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public static int BitwiseCompare<M1, M2>(this ref M1 first, M2 second)
            where M1 : struct, IUnmanagedMemory
            where M2 : IUnmanagedMemory
            => first.Size == second.Size ? first.ToPointer<byte>().BitwiseCompare(second.ToPointer<byte>(), first.Size) : first.Size.CompareTo(second.Size);

        /// <summary>
		/// Gets pointer to the memory block.
		/// </summary>
        /// <param name="memory">Referenced memory.</param>      
		/// <param name="offset">Zero-based byte offset.</param>
		/// <returns>Byte located at the specified offset in the memory.</returns>
		/// <exception cref="NullPointerException">This buffer is not allocated.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid offset.</exception>    
        public static Pointer<byte> ToPointer<M>(this ref M memory, long offset) 
            where M : struct, IUnmanagedMemory
            => offset >= 0 && offset < memory.Size ?
                memory.ToPointer<byte>() + offset :
                throw new ArgumentOutOfRangeException(nameof(offset), offset, ExceptionMessages.InvalidOffsetValue(memory.Size));
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<long> array, long index, long value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<int> array, long index, int value)
            => array.ElementAt(index).VolatileWrite(value);

        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<short> array, long index, short value)
            => array.ElementAt(index).VolatileWrite(value);

        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<byte> array, long index, byte value)
            => array.ElementAt(index).VolatileWrite(value);

        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<bool> array, long index, bool value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<IntPtr> array, long index, IntPtr value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<float> array, long index, float value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        public static void VolatileWrite(this ref UnmanagedArray<double> array, long index, double value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        [CLSCompliant(false)]
        public static void VolatileWrite(this ref UnmanagedArray<ulong> array, long index, ulong value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        [CLSCompliant(false)]
        public static void VolatileWrite(this ref UnmanagedArray<uint> array, long index, uint value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        [CLSCompliant(false)]
        public static void VolatileWrite(this ref UnmanagedArray<ushort> array, long index, ushort value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        [CLSCompliant(false)]
        public static void VolatileWrite(this ref UnmanagedArray<sbyte> array, long index, sbyte value)
            => array.ElementAt(index).VolatileWrite(value);
        
        /// <summary>
        /// Writes array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears before this method in the code, the processor cannot move it after this method.
        /// </remarks>
        /// <param name="array">The unmanaged array.</param>
        /// <param name="index">The index of the array element to be modified.</param>
        /// <param name="value">The value to be saved as array element.</param>
        [CLSCompliant(false)]
        public static void VolatileWrite(this ref UnmanagedArray<UIntPtr> array, long index, UIntPtr value)
            => array.ElementAt(index).VolatileWrite(value);
        
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
        public static void VolatileWrite(this Pointer<long> pointer, long value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<int> pointer, int value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<float> pointer, float value) => Volatile.Write(ref pointer.Ref, value);

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
        public static void VolatileWrite(this Pointer<double> pointer, double value) => Volatile.Write(ref pointer.Ref, value);

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
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static long VolatileRead(this ref UnmanagedArray<long> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static int VolatileRead(this ref UnmanagedArray<int> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static short VolatileRead(this ref UnmanagedArray<short> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static byte VolatileRead(this ref UnmanagedArray<byte> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static float VolatileRead(this ref UnmanagedArray<float> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static double VolatileRead(this ref UnmanagedArray<double> array, long index)
            => array.ElementAt(index).VolatileRead();

        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static bool VolatileRead(this ref UnmanagedArray<bool> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        public static IntPtr VolatileRead(this ref UnmanagedArray<IntPtr> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        [CLSCompliant(false)]
        public static ulong VolatileRead(this ref UnmanagedArray<ulong> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        [CLSCompliant(false)]
        public static uint VolatileRead(this ref UnmanagedArray<uint> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        [CLSCompliant(false)]
        public static ushort VolatileRead(this ref UnmanagedArray<ushort> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        [CLSCompliant(false)]
        public static sbyte VolatileRead(this ref UnmanagedArray<sbyte> array, long index)
            => array.ElementAt(index).VolatileRead();
        
        /// <summary>
        /// Reads array element using volatile semantics.
        /// </summary>
        /// <remarks>
        /// On systems that require it, inserts a memory barrier that prevents the processor from reordering memory operations as follows: 
        /// If a read or write appears after this method in the code, the processor cannot move it before this method.
        /// </remarks>
        /// <param name="array">An unmanaged array.</param>
        /// <param name="index">An index of element to read.</param>
        /// <returns>The value of the array element.</returns>
        [CLSCompliant(false)]
        public static UIntPtr VolatileRead(this ref UnmanagedArray<UIntPtr> array, long index)
            => array.ElementAt(index).VolatileRead();

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
        public static long VolatileRead(this Pointer<long> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static int VolatileRead(this Pointer<int> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static float VolatileRead(this Pointer<float> pointer) => Volatile.Read(ref pointer.Ref);

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
        public static double VolatileRead(this Pointer<double> pointer) => Volatile.Read(ref pointer.Ref);

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
    }
}