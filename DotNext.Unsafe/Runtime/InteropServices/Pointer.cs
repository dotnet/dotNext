using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.IO;

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
        /// Represents zero pointer.
        /// </summary>
        public static Pointer<T> Null => new Pointer<T>(IntPtr.Zero);

        /// <summary>
        /// Size of type <typeparamref name="T"/>, in bytes.
        /// </summary>
        public static int Size => ValueType<T>.Size;

        private readonly T* value;

        [CLSCompliant(false)]
        public Pointer(T* ptr) => this.value = ptr;

        [CLSCompliant(false)]
        public Pointer(void* ptr) => this.value = (T*)ptr;

        public Pointer(IntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        [CLSCompliant(false)]
        public Pointer(UIntPtr ptr)
            : this(ptr.ToPointer())
        {
        }

        public Pointer(ref T value)
            : this(Unsafe.AsPointer<T>(ref value))
        {
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
        /// <param name="length">Number of elements in the unmanaged array.</param>
        public void Clear(int length)
        {
            if(IsNull)
                throw new NullPointerException();
            else if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));
            else
                Unsafe.InitBlockUnaligned(value, 0, (uint)(length * Size));
        }

        /// <summary>
        /// Copies block of memory from the source address to the destination address.
        /// </summary>
        /// <param name="destination">Destination address.</param>
        /// <param name="length">Number of elements to copy.</param>
        public void WriteTo(Pointer<T> destination, long length)
        {
            if(IsNull)
                throw new NullPointerException("Source pointer is null");
            else if(destination.IsNull)
                throw new ArgumentNullException(nameof(destination), "Destination pointer is null");
            else
                Memory.Copy(value, destination.value, length * Size);
        }

        public long WriteTo(T[] destination, long offset, long length)
        {
            if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
            else if (length < 0)
				throw new IndexOutOfRangeException("Destination length is invalid");
			else if (destination.LongLength == 0L || (offset + length) >= destination.LongLength)
				return 0L;
			fixed (T* dest = &destination[offset])
				Memory.Copy(value, dest, length * Size);
			return length;
        }

        public void WriteTo(Stream destination, long length)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				Memory.WriteToSteam(value, length * Size, destination);
        }

        public Task WriteToAsync(Stream destination, long length)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (destination is null)
				throw new ArgumentNullException(nameof(destination));
			else
				return Memory.WriteToSteamAsync(value, length * Size, destination);
        }

        public long ReadFrom(T[] source, long offset, long length)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
            else if (length < 0L)
				throw new IndexOutOfRangeException("Source length is invalid");
			else if (source.LongLength == 0L || (length + offset) >= source.LongLength)
				return 0L;
			fixed (T* src = &source[offset])
				Memory.Copy(src, value, length * Size);
			return length;
		}

        public long ReadFrom(Stream source, long length)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStream(source, value, Size * length);
		}

		public Task<long> ReadFromAsync(Stream source, long length)
		{
			if (IsNull)
				throw new NullPointerException();
			else if (source is null)
				throw new ArgumentNullException(nameof(source));
			else
				return Memory.ReadFromStreamAsync(source, value, Size * length);
		}

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnmanagedMemoryStream AsStream(long length)
            => new UnmanagedMemoryStream(As<byte>().value, length * Size);

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
        public bool IsNull => value == Memory.NullPtr;

        /// <summary>
        /// Reinterprets pointer type.
        /// </summary>
        /// <typeparam name="U">A new pointer type.</typeparam>
        /// <returns>Reinterpreted pointer type.</returns>
        /// <exception cref="GenericArgumentException{U}">Type <typeparamref name="U"/> should be the same size or less than type <typeparamref name="T"/>.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Pointer<U> As<U>()
            where U: unmanaged
            => Size <= Pointer<U>.Size ? new Pointer<U>(value) : throw new GenericArgumentException<U>("Target type should be the same size or less than original type");
        
        /// <summary>
        /// Converts unmanaged pointer into managed pointer.
        /// </summary>
        /// <returns>Managed pointer.</returns>
        /// <exception cref="NullPointerException">This pointer is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T AsRef()
        {
            if(IsNull)
                throw new NullPointerException();
            else
                return ref Unsafe.AsRef<T>(value);
        }
        
        /// <summary>
        /// Reads a value of type <typeparamref name="T"/> from the current location.
        /// </summary>
        /// <param name="access">Memory access mode.</param>
        /// <returns>Dereferenced value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T Read(MemoryAccess access)
        {
            if(IsNull)
                 throw new NullPointerException();
            switch(access)
            {
                case MemoryAccess.Aligned:
                    return Unsafe.Read<T>(value);
                case MemoryAccess.Unaligned:
                    return Unsafe.ReadUnaligned<T>(value);
                default:
                    return *value;
            }
        }
        
        /// <summary>
        /// Writes a value of type <typeparamref name="T"/> to the current location
        /// assuming architecture dependent alignment of the addresses.
        /// </summary>
        /// <param name="access">Memory access mode.</param>
        /// <param name="value">A value to be placed to the current location.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Write(MemoryAccess access, T value)
        {
            if(IsNull)
                throw new NullPointerException();
            switch(access)
            {
                case MemoryAccess.Aligned:
                    Unsafe.Write(this.value, value);
                    return;
                case MemoryAccess.Unaligned:
                    Unsafe.WriteUnaligned(this.value, value);
                    return;
                default:
                    *this.value = value;
                    return;
            }
        }

        public bool BitwiseEquals(Pointer<T> other, int length)
        {
            if (value == other.value)
				return true;
			else if (value == Memory.NullPtr || other.value == Memory.NullPtr)
				return false;
			else
				return Memory.Equals(value, other, length * Size);
        }

        public int BitwiseHashCode(long length, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, length * Size, salted);

        public int BitwiseHashCode(long length, int hash, Func<int, int, int> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, length * Size, hash, hashFunction, salted);
        
        public long BitwiseHashCode(long length, long hash, Func<long, long, long> hashFunction, bool salted = true)
            => IsNull ? 0 : Memory.GetHashCode(value, length * Size, hash, hashFunction, salted);

        public int BitwiseCompare(Pointer<T> other, int length)
        {
            if(IsNull)
                throw new NullPointerException();
            else if(other.value == Memory.NullPtr)
                throw new ArgumentNullException(nameof(other));
            else
                return Memory.Compare(value, other, length * Size);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pointer<T> operator +(Pointer<T> pointer, int offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]        
        public static Pointer<T> operator -(Pointer<T> pointer, int offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pointer<T> operator +(Pointer<T> pointer, long offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pointer<T> operator -(Pointer<T> pointer, long offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pointer<T> operator +(Pointer<T> pointer, ulong offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value + offset);

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Pointer<T> operator -(Pointer<T> pointer, ulong offset)
            => pointer.IsNull ? throw new NullPointerException() : new Pointer<T>(pointer.value - offset);

        public static Pointer<T> operator++(Pointer<T> pointer) => pointer + 1;

        public static Pointer<T> operator--(Pointer<T> pointer) => pointer -1;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(Pointer<T> first, Pointer<T> second)
            => first.Equals(second);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(Pointer<T> first, Pointer<T> second)
            => !first.Equals(second);

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator Pointer<T>(T* value) => new Pointer<T>(value);

        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator T*(Pointer<T> ptr) => ptr.value;

		public static bool operator true(Pointer<T> ptr) => !ptr.IsNull;

		public static bool operator false(Pointer<T> ptr) => ptr.IsNull;

        bool IEquatable<Pointer<T>>.Equals(Pointer<T> other) => Equals(other);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals<U>(Pointer<U> other)
            where U: unmanaged
            => value == other.value;
        
        public bool Equals(T other, IEqualityComparer<T> comparer)
            => !IsNull && comparer.Equals(*value, other);

        public int GetHashCode(IEqualityComparer<T> comparer)
            => IsNull ? 0 : comparer.GetHashCode(*value); 
        
        public override int GetHashCode() => Address.ToInt32();

        public override bool Equals(object other) => other is Pointer<T> ptr && Equals(ptr);

        public override string ToString() => Address.ToString("X");
    }
}