using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// CLS-compliant typed pointer for .NET languages without direct support of pointer data type.
    /// </summary>
    /// <remarks>
    /// Many methods associated with the pointer are unsafe and can destabilize runtime.
    /// Moreover, pointer type doesn't provide automatic memory management.
    /// Null-pointer is the only check performed by methods.
    /// </remarks>
    public readonly struct Pointer<T> : IEquatable<Pointer<T>>
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
                get => *(ptr + index);
            }

            /// <summary>
            /// Adjust pointer.
            /// </summary>
            /// <returns><see langword="true"/>, if next element is available; <see langword="false"/>, if end of sequence reached.</returns>
            public bool MoveNext()
            {
                if (ptr == Memory.NullPtr)
                    return false;
                index += 1L;
                return index < count;
            }

            /// <summary>
            /// Sets the enumerator to its initial position.
            /// </summary>
            public void Reset() => index = -1L;

            /// <summary>
            /// Releases all resources with this enumerator.
            /// </summary>
            public void Dispose() => this = default;
        }

        /// <summary>
        /// Represents zero pointer.
        /// </summary>
        public static Pointer<T> Null => new Pointer<T>(IntPtr.Zero);

        /// <summary>
        /// Size of type <typeparamref name="T"/>, in bytes.
        /// </summary>
        public static int Size => ValueType<T>.Size;

        private unsafe readonly T* value;

        /// <summary>
        /// Constructs CLS-compliant pointer from non CLS-compliant pointer.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public unsafe Pointer(T* ptr) => value = ptr;

        /// <summary>
        /// Constructs CLS-compliant pointer from non CLS-compliant untyped pointer.
        /// </summary>
        /// <param name="ptr">The untyped pointer value.</param>
        [CLSCompliant(false)]
        public unsafe Pointer(void* ptr) => value = (T*)ptr;

        /// <summary>
        /// Constructs pointer from <see cref="IntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        public unsafe Pointer(IntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        /// <summary>
        /// Constructs pointer from <see cref="UIntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public unsafe Pointer(UIntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        /// <summary>
        /// Fills the elements of the array with a specified value.
        /// </summary>
        /// <param name="value">The value to assign to each element of the array.</param>
        /// <param name="count">The length of the array.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is lesst than zero.</exception>
        public unsafe void Fill(T value, long count)
        {
            if (this.value == Memory.NullPtr)
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
		/// Gets or sets pointer value at the specified position in the memory.
		/// </summary>
        /// <remarks>
        /// This property doesn't check bounds of the array.      
        /// </remarks>              
		/// <param name="index">Element index.</param>
		/// <returns>Array element.</returns>
		/// <exception cref="NullPointerException">This array is not allocated.</exception>
        public unsafe T this[long index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => value == Memory.NullPtr ? throw new NullPointerException() : *(value + index);

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (this.value == Memory.NullPtr)
                    throw new NullPointerException();
                else
                    *(this.value + index) = value;
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
            var tmp = *value;
            *value = *other.value;
            *other.value = tmp;
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
                Memory.ClearBits(value, count);
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
                Memory.Copy(value, destination.value, count * Size);
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
                Memory.Copy(value, dest, count * Size);
            return count;
        }

        private static void WriteToSteam(IntPtr source, long length, Stream destination)
        {
            for (var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
            {
                Unsafe.As<byte, IntPtr>(ref buffer[0]) = Memory.ReadUnaligned<IntPtr>(ref source);
                destination.Write(buffer, 0, buffer.Length);
            }
            while (length > 0)
            {
                destination.WriteByte(Memory.Read<byte>(ref source));
                length -= sizeof(byte);
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
        public void WriteTo(Stream destination, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (!destination.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
            else if (count == 0)
                return;
            else
                WriteToSteam(Address, count * Size, destination);
        }

        private static async Task WriteToSteamAsync(IntPtr source, long length, Stream destination)
        {
            for (var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
            {
                Unsafe.As<byte, IntPtr>(ref buffer[0]) = Memory.ReadUnaligned<IntPtr>(ref source);
                await destination.WriteAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
            }
            while (length > 0)
            {
                destination.WriteByte(Memory.Read<byte>(ref source));
                length -= sizeof(byte);
            }
        }

        /// <summary>
        /// Copies bytes from the memory location identified
        /// by this pointer to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="ArgumentException">The stream is not writable.</exception>
        public Task WriteToAsync(Stream destination, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (!destination.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
            else if (count == 0)
                return Task.CompletedTask;
            else
                return WriteToSteamAsync(Address, count * Size, destination);
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
                Memory.Copy(src, value, count * Size);
            return count;
        }

        private static long ReadFromStream(Stream source, IntPtr destination, long length)
        {
            var total = 0L;
            for (var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
            {
                var count = source.Read(buffer, 0, buffer.Length);
                Memory.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
                total += count;
                if (count < IntPtr.Size)
                    return total;
                buffer.Initialize();
            }
            while (length > 0)
            {
                var b = source.ReadByte();
                if (b >= 0)
                {
                    Memory.Write(ref destination, (byte)b);
                    length -= sizeof(byte);
                    total += sizeof(byte);
                }
                else
                    break;
            }
            return total;
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        public long ReadFrom(Stream source, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (!source.CanRead)
                throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
            else if (count == 0L)
                return 0L;
            else
                return ReadFromStream(source, Address, Size * count);
        }

        private static async Task<long> ReadFromStreamAsync(Stream source, IntPtr destination, long length)
        {
            var total = 0L;
            for (var buffer = new byte[IntPtr.Size]; length > IntPtr.Size; length -= IntPtr.Size)
            {
                var count = await source.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                Memory.WriteUnaligned(ref destination, Unsafe.ReadUnaligned<IntPtr>(ref buffer[0]));
                total += count;
                if (count < IntPtr.Size)
                    return total;
                buffer.Initialize();
            }
            while (length > 0)
            {
                var b = source.ReadByte();
                if (b >= 0)
                {
                    Memory.Write(ref destination, (byte)b);
                    length -= sizeof(byte);
                    total += sizeof(byte);
                }
                else
                    break;
            }
            return total;
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
		public Task<long> ReadFromAsync(Stream source, long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0L)
                throw new ArgumentOutOfRangeException(nameof(count));
            else if (!source.CanRead)
                throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));
            else if (count == 0L)
                return Task.FromResult(0L);
            else
                return ReadFromStreamAsync(source, Address, Size * count);
        }

        /// <summary>
        /// Returns representation of the memory identified by this pointer in the form of the stream.
        /// </summary>
        /// <remarks>
        /// This method returns <see cref="Stream"/> compatible over the memory identified by this pointer. No copying is performed.
        /// </remarks>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this memory.</param>
        /// <returns>The stream representing the memory identified by this pointer.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe UnmanagedMemoryStream AsStream(long count)
            => new UnmanagedMemoryStream(As<byte>().value, count * Size);

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
            var result = new byte[Size * length];
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
            get => value == Memory.NullPtr;
        }

        /// <summary>
        /// Reinterprets pointer type.
        /// </summary>
        /// <typeparam name="U">A new pointer type.</typeparam>
        /// <returns>Reinterpreted pointer type.</returns>
        /// <exception cref="GenericArgumentException{U}">Type <typeparamref name="U"/> should be the same size or less than type <typeparamref name="T"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe Pointer<U> As<U>()
            where U : unmanaged
            => Size >= Pointer<U>.Size ? new Pointer<U>(value) : throw new GenericArgumentException<U>(ExceptionMessages.WrongTargetTypeSize, nameof(U));

        /// <summary>
        /// Converts unmanaged pointer into managed pointer.
        /// </summary>
        /// <returns>Managed pointer.</returns>
        /// <exception cref="NullPointerException">This pointer is null.</exception>
        public unsafe ref T Ref
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if (IsNull)
                    throw new NullPointerException();
                else
                    return ref Unsafe.AsRef<T>(value);
            }
        }

        /// <summary>
        /// Gets or sets value stored in the memory identified by this pointer.
        /// </summary>
        public unsafe T Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => IsNull ? throw new NullPointerException() : *value;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if (IsNull)
                    throw new NullPointerException();
                else
                    *this.value = value;
            }
        }

        /// <summary>
        /// Gets enumerator over raw memory.
        /// </summary>
        /// <param name="length">A number of elements to iterate.</param>
        /// <returns>Iterator object.</returns>
        public unsafe Enumerator GetEnumerator(long length) => new Enumerator(value, length);

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
            else if (value == Memory.NullPtr || other.value == Memory.NullPtr)
                return false;
            else
                return Memory.Equals(value, other, count * Size);
        }

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe int BitwiseHashCode(long count, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode32(value, count * Size, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe long BitwiseHashCode64(long count, bool salted = true)
            => IsNull ? 0L : Memory.GetHashCode64(value, count * Size, salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe int BitwiseHashCode(long count, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode32(value, count * Size, hash, hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public unsafe long BitwiseHashCode64(long count, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode64(value, count * Size, hash, hashFunction, salted);

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
            else if (other.value == Memory.NullPtr)
                throw new ArgumentNullException(nameof(other));
            else
                return Memory.Compare(value, other, count * Size);
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
        public unsafe static Pointer<T> operator +(Pointer<T> pointer, int offset)
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
        public unsafe static Pointer<T> operator -(Pointer<T> pointer, int offset)
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static Pointer<T> operator +(Pointer<T> pointer, long offset)
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
        public unsafe static Pointer<T> operator -(Pointer<T> pointer, long offset)
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
        public unsafe static Pointer<T> operator +(Pointer<T> pointer, ulong offset)
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
        public unsafe static Pointer<T> operator -(Pointer<T> pointer, ulong offset)
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
        public unsafe static implicit operator Pointer<T>(T* value) => new Pointer<T>(value);

        /// <summary>
        /// Converts CLS-compliant pointer into its non CLS-compliant representation. 
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static implicit operator T* (Pointer<T> ptr) => ptr.value;

        /// <summary>
        /// Obtains pointer value (address) as <see cref="IntPtr"/>.
        /// </summary>
        /// <param name="ptr">The pointer to be converted.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static implicit operator IntPtr(Pointer<T> ptr) => ptr.Address;

        /// <summary>
        /// Obtains pointer value (address) as <see cref="UIntPtr"/>.
        /// </summary>
        /// <param name="ptr">The pointer to be converted.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [CLSCompliant(false)]
        public unsafe static implicit operator UIntPtr(Pointer<T> ptr) => new UIntPtr(ptr.value);

        /// <summary>
        /// Checks whether this pointer is not zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is not zero; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool operator true(Pointer<T> ptr) => ptr.value != Memory.NullPtr;

        /// <summary>
        /// Checks whether this pointer is zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is zero; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe static bool operator false(Pointer<T> ptr) => ptr.value == Memory.NullPtr;

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