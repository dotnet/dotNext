using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.IO;
using System.Collections;

namespace DotNext.Runtime.InteropServices
{
    /// <summary>
    /// CLS-compliant typed pointer for .NET languages
    /// without direct support of pointer data type.
    /// </summary>
    /// <remarks>
    /// Many methods associated with the pointer are unsafe and can destabilize runtime.
    /// Moreover, pointer type doesn't provide automatic memory management.
    /// Null-pointer is the only check performed by methods.
    /// </remarks>
    public unsafe readonly struct Pointer<T>: IEquatable<Pointer<T>>
        where T: unmanaged
    {
        /// <summary>
        /// Represents enumerator over raw memory.
        /// </summary>
        public struct Enumerator: IEnumerator<T>
        {
            private readonly long count;
            private long index;
            private readonly Pointer<T> ptr;
            
            object IEnumerator.Current => Current;

            internal Enumerator(Pointer<T> ptr, long count)
            {
                this.count = count;
                this.ptr = ptr;
                index = -1L;
            }

            /// <summary>
            /// Pointer to the currently enumerating element.
            /// </summary>
            public Pointer<T> Pointer => ptr + index;

            /// <summary>
            /// Current element.
            /// </summary>
            public T Current => Pointer.Value;

            /// <summary>
            /// Adjust pointer.
            /// </summary>
            /// <returns><see langword="true"/>, if next element is available; <see langword="false"/>, if end of sequence reached.</returns>
            public bool MoveNext()
            {
                if (ptr.IsNull)
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

        private readonly T* value;

        /// <summary>
        /// Constructs CLS-compliant pointer from non CLS-compliant pointer.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public Pointer(T* ptr) => value = ptr;

        /// <summary>
        /// Constructs CLS-compliant pointer from non CLS-compliant untyped pointer.
        /// </summary>
        /// <param name="ptr">The untyped pointer value.</param>
        [CLSCompliant(false)]
        public Pointer(void* ptr) => value = (T*)ptr;

        /// <summary>
        /// Constructs pointer from <see cref="IntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        public Pointer(IntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        /// <summary>
        /// Constructs pointer from <see cref="UIntPtr"/> value.
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        public Pointer(UIntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        /// <summary>
        /// Swaps values between this memory location and the given memory location.
        /// </summary>
        /// <param name="other">The other memory location.</param>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> pointer is zero.</exception>
        public void Swap(Pointer<T> other)
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
        /// <param name="length">length of unmanaged memory array.</param>
        [CLSCompliant(false)]
        public void Clear(uint length)
        {
            if(IsNull)
                throw new NullPointerException();
            else
                Unsafe.InitBlockUnaligned(value, 0, length * (uint)Size);
        }

        /// <summary>
        /// Fill memory with zero bytes.
        /// </summary>
        /// <param name="count">Number of elements in the unmanaged array.</param>
        public void Clear(long count)
        {
            if (IsNull)
                throw new NullPointerException();
            else if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            else
                Memory.ZeroMem(value, count);
        }

        /// <summary>
        /// Copies block of memory from the source address to the destination address.
        /// </summary>
        /// <param name="destination">Destination address.</param>
        /// <param name="count">The number of elements to be copied.</param>
        public void WriteTo(Pointer<T> destination, long count)
        {
            if(IsNull)
                throw new NullPointerException(ExceptionMessages.NullSource);
            else if(destination.IsNull)
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
        public long WriteTo(T[] destination, long offset, long count)
        {
            if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
            else if (count < 0)
				throw new IndexOutOfRangeException();
			else if (destination.LongLength == 0L || (offset + count) > destination.LongLength)
				return 0L;
			fixed (T* dest = &destination[offset])
				Memory.Copy(value, dest, count * Size);
			return count;
        }

        /// <summary>
        /// Copies bytes from the memory location identified by this pointer to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        public void WriteTo(Stream destination, long count)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				Memory.WriteToSteam(value, count * Size, destination);
        }

        /// <summary>
        /// Copies bytes from the memory location identified
        /// by this pointer to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        public Task WriteToAsync(Stream destination, long count)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				return Memory.WriteToSteamAsync(value, count * Size, destination);
        }

        /// <summary>
        /// Copies elements from the specified array into
        /// the memory block identified by this pointer.
        /// </summary>
        /// <param name="source">The source array.</param>
        /// <param name="offset">The position in the source array from which copying begins.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>Actual number of copied elements.</returns>
        public long ReadFrom(T[] source, long offset, long count)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
            else if (count < 0L)
				throw new IndexOutOfRangeException();
			else if (source.LongLength == 0L || (count + offset) > source.LongLength)
				return 0L;
			fixed (T* src = &source[offset])
				Memory.Copy(src, value, count * Size);
			return count;
		}

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        public long ReadFrom(Stream source, long count)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStream(source, value, Size * count);
		}

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
		public Task<long> ReadFromAsync(Stream source, long count)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStreamAsync(source, value, Size * count);
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
        public UnmanagedMemoryStream AsStream(long count)
            => new UnmanagedMemoryStream(As<byte>().value, count * Size);

        /// <summary>
        /// Copies block of memory referenced by this pointer
        /// into managed heap as array of bytes.
        /// </summary>
        /// <param name="length">Number of elements to copy.</param>
        /// <returns>A copy of memory block in the form of byte array.</returns>
        public byte[] ToByteArray(long length)
        {
            if (IsNull)
				return Array.Empty<byte>();
			var result = new byte[Size];
			fixed (byte* destination = result)
				Memory.Copy(value, destination, Size * length);
			return result;
        }

        /// <summary>
        /// Gets pointer address.
        /// </summary>
        public IntPtr Address => new IntPtr(value);

        /// <summary>
        /// Indicates that this pointer is null
        /// </summary>
        public bool IsNull
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
        public Pointer<U> As<U>()
            where U : unmanaged
            => Size <= Pointer<U>.Size ? new Pointer<U>(value) : throw new GenericArgumentException<U>(ExceptionMessages.WrongTargetTypeSize);

        /// <summary>
        /// Converts unmanaged pointer into managed pointer.
        /// </summary>
        /// <returns>Managed pointer.</returns>
        /// <exception cref="NullPointerException">This pointer is null.</exception>
        public ref T Ref
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
        public T Value
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
        public Enumerator GetEnumerator(long length) => new Enumerator(this, length);

        /// <summary>
        /// Computes bitwise equality between two blocks of memory.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by both pointers.</param>
        /// <returns><see langword="true"/>, if both memory blocks have the same bytes; otherwise, <see langword="false"/>.</returns>
        public bool BitwiseEquals(Pointer<T> other, long count)
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
        public int BitwiseHashCode(long count, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, count * Size, salted);

        /// <summary>
        /// Computes 32-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public int BitwiseHashCode(long count, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, count * Size, hash, hashFunction, salted);

        /// <summary>
        /// Computes 64-bit hash code for the block of memory identified by this pointer.
        /// </summary>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this pointer.</param>
        /// <param name="hash">Initial value of the hash to be passed into hashing function.</param>
        /// <param name="hashFunction">The custom hash function.</param>
        /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
        /// <returns>Content hash code.</returns>
        public long BitwiseHashCode(long count, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, count * Size, hash, hashFunction, salted);

        /// <summary>
        /// Bitwise comparison of two memory blocks.
        /// </summary>
        /// <param name="other">The pointer identifies block of memory to be compared.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by both pointers.</param>
        /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
        public int BitwiseCompare(Pointer<T> other, long count)
        {
            if (value == other.value)
                return 0;
            else if(IsNull)
                throw new NullPointerException();
            else if(other.value == Memory.NullPtr)
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
        public static Pointer<T> operator +(Pointer<T> pointer, int offset)
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
        public static Pointer<T> operator -(Pointer<T> pointer, int offset)
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
        public static Pointer<T> operator +(Pointer<T> pointer, long offset)
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
        public static Pointer<T> operator -(Pointer<T> pointer, long offset)
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
        public static Pointer<T> operator +(Pointer<T> pointer, ulong offset)
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
        public static Pointer<T> operator -(Pointer<T> pointer, ulong offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        /// <summary>
        /// Increments this pointer by 1 element of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="pointer">The pointer to add the offset to.</param>
        /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
        public static Pointer<T> operator++(Pointer<T> pointer) => pointer + 1;

        /// <summary>
        /// Decrements this pointer by 1 element of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="pointer">The pointer to subtract the offset from.</param>
        /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
        public static Pointer<T> operator--(Pointer<T> pointer) => pointer -1;

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
        public static implicit operator Pointer<T>(T* value) => new Pointer<T>(value);

        /// <summary>
        /// Converts CLS-compliant pointer into its non CLS-compliant representation. 
        /// </summary>
        /// <param name="ptr">The pointer value.</param>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T*(Pointer<T> ptr) => ptr.value;

        /// <summary>
        /// Checks whether this pointer is not zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is not zero; otherwise, <see langword="false"/>.</returns>
		public static bool operator true(Pointer<T> ptr) => !ptr.IsNull;

        /// <summary>
        /// Checks whether this pointer is zero.
        /// </summary>
        /// <param name="ptr">The pointer to check.</param>
        /// <returns><see langword="true"/>, if this pointer is zero; otherwise, <see langword="false"/>.</returns>
		public static bool operator false(Pointer<T> ptr) => ptr.IsNull;

        bool IEquatable<Pointer<T>>.Equals(Pointer<T> other) => Equals(other);

        /// <summary>
        /// Indicates that this pointer represents the same memory location as other pointer.
        /// </summary>
        /// <typeparam name="U">The type of the another pointer.</typeparam>
        /// <param name="other">The pointer to be compared.</param>
        /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals<U>(Pointer<U> other)
            where U: unmanaged
            => value == other.value;

        /// <summary>
        /// Determines whether the value stored in the memory identified by this pointer is equal to the given value.
        /// </summary>
        /// <param name="other">The value to be compared.</param>
        /// <param name="comparer">The object implementing comparison algorithm.</param>
        /// <returns><see langword="true"/>, if the value stored in the memory identified by this pointer is equal to the given value; otherwise, <see langword="false"/>.</returns>
        public bool Equals(T other, IEqualityComparer<T> comparer)
            => !IsNull && comparer.Equals(*value, other);

        /// <summary>
        /// Computes hash code of the value stored in the memory identified by this pointer.
        /// </summary>
        /// <param name="comparer">The object implementing custom hash function.</param>
        /// <returns>The hash code of the value stored in the memory identified by this pointer.</returns>
        public int GetHashCode(IEqualityComparer<T> comparer)
            => IsNull ? 0 : comparer.GetHashCode(*value);

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