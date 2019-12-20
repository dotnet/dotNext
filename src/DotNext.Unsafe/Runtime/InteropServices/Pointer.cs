using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CancellationToken = System.Threading.CancellationToken;
using MemoryHandle = System.Buffers.MemoryHandle;

namespace DotNext.Runtime.InteropServices
{
    using Buffers;
    using Generic;
    using Threading.Tasks;

    /// <summary>
    /// CLS-compliant typed pointer for .NET languages without direct support of pointer data type.
    /// </summary>
    /// <remarks>
    /// Many methods associated with the pointer are unsafe and can destabilize runtime.
    /// Moreover, pointer type doesn't provide automatic memory management.
    /// Null-pointer is the only check performed by methods.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct Pointer<T> : IEquatable<Pointer<T>>, IStrongBox
        where T : unmanaged
    {
        /// <summary>
        /// Represents enumerator over raw memory.
        /// </summary>
        public unsafe struct Enumerator : IEnumerator<T>
        {
            private readonly long count;
            private long index;
            private readonly T* ptr;

            object IEnumerator.Current => Current;

            internal Enumerator(T* ptr, long count)
            {
                this.count = count;
                this.ptr = ptr;
                index = -1L;
            }

            /// <summary>
            /// Pointer to the currently enumerating element.
            /// </summary>
            public Pointer<T> Pointer
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => new Pointer<T>(ptr + index);
            }

            /// <summary>
            /// Current element.
            /// </summary>
            public T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ptr[index];
            }

            /// <summary>
            /// Adjust pointer.
            /// </summary>
            /// <returns><see langword="true"/>, if next element is available; <see langword="false"/>, if end of sequence reached.</returns>
            public bool MoveNext() => ptr != default && ++index < count;

            /// <summary>
            /// Sets the enumerator to its initial position.
            /// </summary>
            public void Reset() => index = -1L;

            /// <summary>
            /// Releases all resources with this enumerator.
            /// </summary>
            public void Dispose() => this = default;
        }

        private const int BufferSize = 1024;

        /// <summary>
        /// Represents zero pointer.
        /// </summary>
        public static Pointer<T> Null => default;

        private readonly unsafe T* value;

        /// <summary>
        /// Constructs CLS-compliant pointer from non CLS-compliant pointer.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public unsafe Pointer(T* ptr) => value = ptr;

        /// <summary>
        /// Constructs pointer from <see cref="IntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        public unsafe Pointer(IntPtr ptr)
            : this((T*)ptr)
        {
        }

        /// <summary>
        /// Constructs pointer from <see cref="UIntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public unsafe Pointer(UIntPtr ptr)
            : this((T*)ptr)
        {
        }

        /// <summary>
        /// Fills the elements of the array with a specified value.
        /// </summary>
        /// <param name="value">The value to assign to each element of the array.</param>
        /// <param name="count">The length of the array.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        public unsafe void Fill(T value, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (count == 0)
                return;
            var pointer = Address;
            do
            {
                var actualCount = (int)count.Min(int.MaxValue);
                var span = new Span<T>(pointer.ToPointer(), actualCount);
                span.Fill(value);
                count -= actualCount;
                pointer += actualCount;
            }
            while (count > 0);
        }

        /// <summary>
        /// Converts this pointer into <see cref="Span{T}"/>.
        /// </summary>
        /// <param name="length">The number of elements located in the unmanaged memory identified by this pointer.</param>
        /// <returns><see cref="Span{T}"/> representing elements in the unmanaged memory.</returns>
        public unsafe Span<T> ToSpan(int length) => IsNull ? default : new Span<T>(value, length);

        /// <summary>
        /// Converts this pointer into span of bytes.
        /// </summary>
        public unsafe Span<byte> Bytes => IsNull ? default : Memory.AsSpan(value);

        /// <summary>
		/// Gets or sets pointer value at the specified position in the memory.
		/// </summary>
        /// <remarks>
        /// This property doesn't check bounds of the array.      
        /// </remarks>              
		/// <param name="index">Element index.</param>
		/// <returns>Array element.</returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
        public unsafe ref T this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsNull)
                    throw new NullPointerException();
                else
                    return ref value[index];
            }
        }

        /// <summary>
        /// Swaps values between this memory location and the given memory location.
        /// </summary>
        /// <param name="other">The other memory location.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> pointer is zero.</exception>
        public unsafe void Swap(Pointer<T> other)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (other.IsNull)
                throw new ArgumentNullException(nameof(other));
            Memory.Swap(value, other.value);
        }

        unsafe object IStrongBox.Value
        {
            get => *value;
            set => *this.value = (T)value;
        }

        /// <summary>
        /// Fill memory with zero bytes.
        /// </summary>
        /// <param name="count">Number of elements in the unmanaged array.</param>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than or equal to zero.</exception>
        public unsafe void Clear(long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else
                Memory.ClearBits(value, sizeof(T) * count);
        }

        /// <summary>
        /// Copies block of memory from the source address to the destination address.
        /// </summary>
        /// <param name="destination">Destination address.</param>
        /// <param name="count">The number of elements to be copied.</param>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentNullException">Destination pointer is zero.</exception>
        public unsafe void WriteTo(Pointer<T> destination, long count)
        {
            if (IsNull)
                throw new NullPointerException(ExceptionMessages.NullSource);
            else if (destination.IsNull)
                throw new ArgumentNullException(nameof(destination), ExceptionMessages.NullDestination);
            else
                Memory.Copy(value, destination.value, count * sizeof(T));
        }

        /// <summary>
        /// Copies elements from the memory location identified
        /// by this pointer to managed array.
        /// </summary>
        /// <param name="destination">The array to be modified.</param>
        /// <param name="offset">The position in the destination array from which copying begins.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> or <paramref name="offset"/> is less than zero.</exception>
        public unsafe long WriteTo(T[] destination, long offset, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (destination.LongLength == 0L || (offset + count) > destination.LongLength)
                return 0L;
            fixed (T* dest = &destination[offset])
                Memory.Copy(value, dest, count * sizeof(T));
            return count;
        }

        private unsafe static void WriteToSteam(byte* source, long length, Stream destination)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var offset = 0L;
            using (var buffer = new ArrayRental<byte>((int)Math.Min(BufferSize, length)))
                while (length > 0L)
                {
                    //copy from memory to buffer
                    var count = (int)Math.Min(length, buffer.Length);
                    Unsafe.CopyBlockUnaligned(ref buffer[0], ref source[offset], (uint)count);
                    //copy from buffer to stream
                    destination.Write((byte[])buffer, 0, count);
                    length -= count;
                    offset += count;
                }
        }

        /// <summary>
        /// Copies bytes from the memory location identified by this pointer to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentException">The stream is not writable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        public unsafe void WriteTo(Stream destination, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!destination.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
            if (count > 0)
                WriteToSteam((byte*)value, count * sizeof(T), destination);
        }

        private static async Task WriteToSteamAsync(IntPtr source, long length, Stream destination, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var offset = 0L;
            using (var buffer = new ArrayRental<byte>((int)Math.Min(BufferSize, length)))
                while (length > 0L)
                {
                    //copy from memory to buffer
                    var count = (int)Math.Min(length, buffer.Length);
                    unsafe
                    {
                        Unsafe.CopyBlockUnaligned(ref buffer[0], ref source.ToPointer<byte>()[offset], (uint)count);
                    }
                    //copy from buffer to stream
                    await destination.WriteAsync((byte[])buffer, 0, count, token).ConfigureAwait(false);
                    length -= count;
                    offset += count;
                }
        }

        /// <summary>
        /// Copies bytes from the memory location identified
        /// by this pointer to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="ArgumentException">The stream is not writable.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public unsafe Task WriteToAsync(Stream destination, long count, CancellationToken token = default)
        {
            if (IsNull)
                throw new NullPointerException();
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!destination.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
            if (count == 0)
                return Task.CompletedTask;
            return WriteToSteamAsync(Address, count * sizeof(T), destination, token);
        }

        /// <summary>
        /// Copies elements from the specified array into
        /// the memory block identified by this pointer.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> or <paramref name="offset"/> is less than zero.</exception>
        public unsafe long ReadFrom(T[] source, long offset, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (offset < 0L)
                throw new ArgumentOutOfRangeException(nameof(offset));
            else if (source.LongLength == 0L || (count + offset) > source.LongLength)
                return 0L;
            fixed (T* src = &source[offset])
                Memory.Copy(src, value, count * sizeof(T));
            return count;
        }

        private unsafe static long ReadFromStream(Stream source, byte* destination, long length)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var total = 0L;
            using (var buffer = new ArrayRental<byte>((int)Math.Min(BufferSize, length)))
                while (length > 0L)
                {
                    //copy from stream to buffer
                    var bytesRead = source.Read((byte[])buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;
                    //copy from buffer to memory
                    Unsafe.CopyBlockUnaligned(ref destination[total], ref buffer[0], (uint)bytesRead);
                    length -= bytesRead;
                    total += bytesRead;
                }
            return total;
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        public unsafe long ReadFrom(Stream source, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!source.CanRead)
                throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
            if (count == 0L)
                return 0L;
            return ReadFromStream(source, (byte*)value, sizeof(T) * count);
        }

        private static async Task<long> ReadFromStreamAsync(Stream source, IntPtr destination, long length, CancellationToken token)
        {
            //TODO: Should be rewritten for .NET Standard 2.1
            var total = 0L;
            using (var buffer = new ArrayRental<byte>((int)Math.Min(BufferSize, length)))
                while (length > 0L)
                {
                    //copy from stream to buffer
                    var bytesRead = await source.ReadAsync((byte[])buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead == 0)
                        break;
                    //copy from buffer to memory
                    unsafe
                    {
                        Unsafe.CopyBlock(ref destination.ToPointer<byte>()[total], ref buffer[0], (uint)bytesRead);
                    }
                    length -= bytesRead;
                    total += bytesRead;
                }
            return total;
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
		public unsafe Task<long> ReadFromAsync(Stream source, long count, CancellationToken token = default)
        {
            if (IsNull)
                throw new NullPointerException();
            if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            if (!source.CanRead)
                throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
            if (count == 0L)
                return CompletedTask<long, LongConst.Zero>.Task;
            return ReadFromStreamAsync(source, Address, sizeof(T) * count, token);
        }

        /// <summary>
        /// Returns representation of the memory identified by this pointer in the form of the stream.
        /// </summary>
        /// <remarks>
        /// This method returns <see cref="Stream"/> compatible over the memory identified by this pointer. No copying is performed.
        /// </remarks>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this memory.</param>
        /// <param name="access">The type of the access supported by the returned stream.</param>
        /// <returns>The stream representing the memory identified by this pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Stream AsStream(long count, FileAccess access = FileAccess.ReadWrite)
        {
            if (IsNull)
                return Stream.Null;
            count *= sizeof(T);
            return new UnmanagedMemoryStream(As<byte>().value, count, count, access);
        }

        /// <summary>
        /// Copies block of memory referenced by this pointer
        /// into managed heap as array of bytes.
        /// </summary>
        /// <param name="length">Number of elements to copy.</param>
        /// <returns>A copy of memory block in the form of byte array.</returns>
        public unsafe byte[] ToByteArray(long length)
        {
            if (IsNull)
                return Array.Empty<byte>();
            var result = new byte[sizeof(T) * length];
            fixed (byte* destination = result)
                Memory.Copy(value, destination, result.LongLength);
            return result;
        }

        /// <summary>
        /// Gets pointer address.
        /// </summary>
        public unsafe IntPtr Address
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new IntPtr(value);
        }

        /// <summary>
        /// Indicates that this pointer is <see langword="null"/>.
        /// </summary>
        public unsafe bool IsNull
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value == default;
        }

        /// <summary>
        /// Reinterprets pointer type.
        /// </summary>
        /// <typeparam name="U">A new pointer type.</typeparam>
        /// <returns>Reinterpreted pointer type.</returns>
        /// <exception cref="GenericArgumentException{U}">Type <typeparamref name="U"/> should be the same size or less than type <typeparamref name="T"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SuppressMessage("Usage", "CA2208", Justification = "The name of the generic parameter is correct")]
        public unsafe Pointer<U> As<U>()
            where U : unmanaged
            => sizeof(T) >= sizeof(U) ? new Pointer<U>(Address) : throw new GenericArgumentException<U>(ExceptionMessages.WrongTargetTypeSize, nameof(U));

        /// <summary>
        /// Gets the value stored in the memory identified by this pointer.
        /// </summary>
        /// <value>The reference to the memory location.</value>
        /// <exception cref="NullPointerException">The pointer is 0.</exception>
        public unsafe ref T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsNull)
                    throw new NullPointerException();
                else
                    return ref value[0];
            }
        }

        /// <summary>
        /// Gets the value stored in the memory identified by this pointer.
        /// </summary>
        /// <returns>The value stored in the memory.</returns>
        /// <exception cref="NullPointerException">The pointer is 0.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get() => Value;

        /// <summary>
        /// Gets the value stored in the memory at the specified position.
        /// </summary>
        /// <param name="index">The index of the element.</param>
        /// <returns>The value stored in the memory at the specified position.</returns>
        /// <exception cref="NullPointerException">The pointer is 0.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Get(long index) => this[index];

        /// <summary>
        /// Sets the value stored in the memory identified by this pointer.
        /// </summary>
        /// <param name="value">The value to be stored in the memory.</param>
        /// <exception cref="NullPointerException">The pointer is 0.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Set(T value) => Value = value;

        /// <summary>
        /// Sets the value at the specified position in the memory.
        /// </summary>
        /// <param name="value">The value to be stored in the memory.</param>
        /// <param name="index">The index of the element to modify.</param>
        /// <exception cref="NullPointerException">The pointer is 0.</exception>
        public void Set(T value, long index) => this[index] = value;

        /// <summary>
        /// Gets enumerator over raw memory.
        /// </summary>
        /// <param name="length">A number of elements to iterate.</param>
        /// <returns>Iterator object.</returns>
        public unsafe Enumerator GetEnumerator(long length) => IsNull ? default : new Enumerator(value, length);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by both pointers.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public unsafe bool BitwiseEquals(Pointer<T> other, long count)
        {
            if (value == other.value)
                return true;
            else if (IsNull || other.IsNull)
                return false;
            else
                return Memory.Equals(value, other, count * sizeof(T));
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe int BitwiseHashCode(long count, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode32(value, count * sizeof(T), salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe long BitwiseHashCode64(long count, bool salted = true)
            => IsNull ? 0L : Memory.GetHashCode64(value, count * sizeof(T), salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public int BitwiseHashCode(long count, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => BitwiseHashCode(count, hash, new ValueFunc<int, int, int>(hashFunction, true), salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe int BitwiseHashCode(long count, int hash, in ValueFunc<int, int, int> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode32(value, count * sizeof(T), hash, hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe long BitwiseHashCode64(long count, long hash, in ValueFunc<long, long, long> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode64(value, count * sizeof(T), hash, hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public long BitwiseHashCode64(long count, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => BitwiseHashCode64(count, hash, new ValueFunc<long, long, long>(hashFunction, true), salted);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by both pointers.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public unsafe int BitwiseCompare(Pointer<T> other, long count)
        {
            if (value == other.value)
                return 0;
            else if (IsNull)
                throw new NullPointerException();
            else if (other.IsNull)
                throw new ArgumentNullException(nameof(other));
            else
                return Memory.Compare(value, other, count * sizeof(T));
        }

        /// <summary>
        /// Adds an offset to the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <param name="offset">The offset to add.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator +(Pointer<T> pointer, int offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        /// <summary>
        /// Adds an offset to the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <param name="offset">The offset to add.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator +(Pointer<T> pointer, IntPtr offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.Address.Add(offset));

        /// <summary>
        /// Subtracts an offset from the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <param name="offset">The offset to subtract.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator -(Pointer<T> pointer, int offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        /// <summary>
        /// Subtracts an offset from the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <param name="offset">The offset to subtract.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator -(Pointer<T> pointer, IntPtr offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.Address.Subtract(offset));

        /// <summary>
        /// Adds an offset to the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <param name="offset">The offset to add.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator +(Pointer<T> pointer, long offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        /// <summary>
        /// Subtracts an offset from the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <param name="offset">The offset to subtract.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator -(Pointer<T> pointer, long offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        /// <summary>
        /// Adds an offset to the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <param name="offset">The offset to add.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator +(Pointer<T> pointer, ulong offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        /// <summary>
        /// Subtracts an offset from the value of a pointer.
        /// </summary>
        /// <remarks>
        /// The offset specifies number of elements of type <typeparamref name="T"/>, not bytes.
        /// </remarks>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <param name="offset">The offset to subtract.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Pointer<T> operator -(Pointer<T> pointer, ulong offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        /// <summary>
        /// Increments this pointer by 1 element of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        public static Pointer<T> operator ++(Pointer<T> pointer) => pointer + 1;

        /// <summary>
        /// Decrements this pointer by 1 element of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        public static Pointer<T> operator --(Pointer<T> pointer) => pointer - 1;

        /// <summary>
        /// Indicates that the first pointer represents the same memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the same memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Pointer<T> first, Pointer<T> second)
            => first.Equals(second);

        /// <summary>
        /// Indicates that the first pointer represents the different memory location as the second pointer.
        /// </summary>
        /// <param name="first">The first pointer to be compared.</param>
        /// <param name="second">The second pointer to be compared.</param>
        /// <returns><see langword="true"/>, if the first pointer represents the different memory location as the second pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Pointer<T> first, Pointer<T> second)
            => !first.Equals(second);

        /// <summary>
        /// Converts non CLS-compliant pointer into its CLS-compliant representation. 
        /// </summary>
        /// <param name="value">The pointer value.</param>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator Pointer<T>(T* value) => new Pointer<T>(value);

        /// <summary>
        /// Converts CLS-compliant pointer into its non CLS-compliant representation. 
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe implicit operator T*(Pointer<T> ptr) => ptr.value;

        /// <summary>
        /// Obtains pointer value (address) as <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="ptr">The pointer to be converted.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator IntPtr(Pointer<T> ptr) => ptr.Address;

        /// <summary>
        /// Obtains pointer value (address) as <see cref="UIntPtr"/>.
        /// </summary>
        /// <param name="ptr">The pointer to be converted.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public static unsafe implicit operator UIntPtr(Pointer<T> ptr) => new UIntPtr(ptr.value);

        /// <summary>
        /// Obtains pointer to the memory represented by given memory handle.
        /// </summary>
        /// <param name="handle">The memory handle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static explicit operator Pointer<T>(in MemoryHandle handle) => new Pointer<T>(new IntPtr(handle.Pointer));

        /// <summary>
        /// Checks whether this pointer is not zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is not zero; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator true(Pointer<T> ptr) => !ptr.IsNull;

        /// <summary>
        /// Checks whether this pointer is zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is zero; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator false(Pointer<T> ptr) => ptr.IsNull;

        bool IEquatable<Pointer<T>>.Equals(Pointer<T> other) => Equals(other);

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <typeparam name="U">The type of the another pointer.</typeparam>
        /// <param name="other">The pointer to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Equals<U>(Pointer<U> other) where U : unmanaged => value == other.value;

        /// <summary>
        /// Determines whether the value stored in the memory identified by this pointer is equal to the given value.
        /// </summary>
        /// <param name="other">The value to be compared.</param>
        /// <param name="comparer">The object implementing comparison algorithm.</param>
        /// <returns><see langword="true"/>, if the value stored in the memory identified by this pointer is equal to the given value; otherwise, <see langword="false"/>.</returns>
        public unsafe bool Equals(T other, IEqualityComparer<T> comparer) => !IsNull && comparer.Equals(*value, other);

        /// <summary>
        /// Computes hash code of the value stored in the memory identified by this pointer.
        /// </summary>
        /// <param name="comparer">The object implementing custom hash function.</param>
        /// <returns>The hash code of the value stored in the memory identified by this pointer.</returns>
        public unsafe int GetHashCode(IEqualityComparer<T> comparer) => IsNull ? 0 : comparer.GetHashCode(*value);

        /// <summary>
        /// Computes hash code of the pointer itself (i.e. address), not of the memory content.
        /// </summary>
        /// <returns>The hash code of this pointer.</returns>
        public override int GetHashCode() => Address.GetHashCode();

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <param name="other">The object of type <see cref="Pointer{T}"/> to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
        public override bool Equals(object other) => other is Pointer<T> ptr && Equals(ptr);

        /// <summary>
        /// Returns hexadecimal address represented by this pointer.
        /// </summary>
        /// <returns>The hexadecimal value of this pointer.</returns>
        public override string ToString() => Address.ToString("X");
    }
}