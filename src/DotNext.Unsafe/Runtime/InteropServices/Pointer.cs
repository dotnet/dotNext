using System.Buffers;
using System.Collections;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CancellationToken = System.Threading.CancellationToken;
using MemoryHandle = System.Buffers.MemoryHandle;
using Pointer = System.Reflection.Pointer;

namespace DotNext.Runtime.InteropServices;

using MemorySource = Buffers.UnmanagedMemory<byte>;

/// <summary>
/// CLS-compliant typed pointer for .NET languages without direct support of pointer data type.
/// </summary>
/// <typeparam name="T">The type of pointer.</typeparam>
/// <remarks>
/// Many methods associated with the pointer are unsafe and can destabilize runtime.
/// Moreover, pointer type doesn't provide automatic memory management.
/// Null-pointer is the only check performed by methods.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Pointer<T> :
    IEquatable<Pointer<T>>,
    IComparable<Pointer<T>>,
    IStrongBox,
    ISupplier<IntPtr>,
    ISupplier<UIntPtr>,
    IPinnable,
    ISpanFormattable,
    IComparisonOperators<Pointer<T>, Pointer<T>, bool>,
    IEqualityOperators<Pointer<T>, Pointer<T>, bool>,
    IAdditionOperators<Pointer<T>, int, Pointer<T>>,
    IAdditionOperators<Pointer<T>, long, Pointer<T>>,
    IAdditionOperators<Pointer<T>, nint, Pointer<T>>,
    IAdditionOperators<Pointer<T>, nuint, Pointer<T>>,
    ISubtractionOperators<Pointer<T>, int, Pointer<T>>,
    ISubtractionOperators<Pointer<T>, long, Pointer<T>>,
    ISubtractionOperators<Pointer<T>, nint, Pointer<T>>,
    ISubtractionOperators<Pointer<T>, nuint, Pointer<T>>,
    IIncrementOperators<Pointer<T>>,
    IDecrementOperators<Pointer<T>>,
    IAdditiveIdentity<Pointer<T>, nint>,
    IAdditiveIdentity<Pointer<T>, nuint>
    where T : unmanaged
{
    /// <summary>
    /// Represents enumerator over raw memory.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public unsafe struct Enumerator : IEnumerator<T>
    {
        private readonly T* ptr;
        private readonly nuint count;
        private nuint index;

        /// <inheritdoc/>
        readonly object IEnumerator.Current => Current;

        internal Enumerator(T* ptr, nuint count)
        {
            this.count = count > 0 ? count : throw new ArgumentOutOfRangeException(nameof(count));
            this.ptr = ptr;
            index = nuint.MaxValue;
        }

        /// <summary>
        /// Pointer to the currently enumerating element.
        /// </summary>
        public readonly Pointer<T> Pointer
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(ptr + index);
        }

        /// <summary>
        /// Current element.
        /// </summary>
        public readonly T Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ptr[index];
        }

        /// <summary>
        /// Adjust pointer.
        /// </summary>
        /// <returns><see langword="true"/>, if next element is available; <see langword="false"/>, if end of sequence reached.</returns>
        public bool MoveNext() => ptr is not null && ++index < count;

        /// <summary>
        /// Sets the enumerator to its initial position.
        /// </summary>
        public void Reset() => index = nuint.MaxValue;

        /// <summary>
        /// Releases all resources with this enumerator.
        /// </summary>
        public void Dispose() => this = default;
    }

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
    public unsafe Pointer(nint ptr)
        : this((T*)ptr)
    {
    }

    /// <summary>
    /// Constructs pointer from <see cref="UIntPtr"/> value.
    /// </summary>
    /// <param name="ptr">The pointer value.</param>
    [CLSCompliant(false)]
    public unsafe Pointer(nuint ptr)
        : this((T*)ptr)
    {
    }

    /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity"/>
    static nint IAdditiveIdentity<Pointer<T>, nint>.AdditiveIdentity => 0;

    /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity"/>
    static nuint IAdditiveIdentity<Pointer<T>, nuint>.AdditiveIdentity => 0U;

    [DoesNotReturn]
    [StackTraceHidden]
    private static void ThrowNullPointerException() => throw new NullPointerException();

    /// <summary>
    /// Gets boxed pointer.
    /// </summary>
    /// <returns>The boxed pointer.</returns>
    /// <seealso cref="Pointer"/>
    [CLSCompliant(false)]
    public unsafe object GetBoxedPointer() => Pointer.Box(value, typeof(T*));

    /// <summary>
    /// Determines whether this pointer is aligned
    /// to the size of <typeparamref name="T"/>.
    /// </summary>
    public unsafe bool IsAligned => Address % Intrinsics.AlignOf<T>() is 0;

    /// <summary>
    /// Fills the elements of the array with a specified value.
    /// </summary>
    /// <param name="value">The value to assign to each element of the array.</param>
    /// <param name="count">The length of the array.</param>
    /// <exception cref="NullPointerException">This pointer is zero.</exception>
    [CLSCompliant(false)]
    public unsafe void Fill(T value, nuint count)
    {
        if (IsNull)
            ThrowNullPointerException();

        var pointer = this.value;
        for (nuint len; count > 0; count -= len, pointer += len)
        {
            len = count > int.MaxValue ? int.MaxValue : count;
            new Span<T>(pointer, (int)len).Fill(value);
        }
    }

    /// <summary>
    /// Converts this pointer into <see cref="Span{T}"/>.
    /// </summary>
    /// <param name="length">The number of elements located in the unmanaged memory identified by this pointer.</param>
    /// <returns><see cref="Span{T}"/> representing elements in the unmanaged memory.</returns>
    public unsafe Span<T> ToSpan(int length) => IsNull ? [] : new(value, length);

    /// <summary>
    /// Converts this pointer into span of bytes.
    /// </summary>
    public unsafe Span<byte> Bytes => IsNull ? [] : Span.AsBytes(value);

    /// <summary>
    /// Gets or sets pointer value at the specified position in the memory.
    /// </summary>
    /// <remarks>
    /// This property doesn't check bounds of the array.
    /// </remarks>
    /// <param name="index">Element index.</param>
    /// <returns>Array element.</returns>
    /// <exception cref="NullPointerException">This array is not allocated.</exception>
    [CLSCompliant(false)]
    public unsafe ref T this[nuint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (IsNull)
                ThrowNullPointerException();

            return ref value[index];
        }
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
    [CLSCompliant(false)]
    public unsafe ref T this[nint index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if (IsNull)
                ThrowNullPointerException();

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
            ThrowNullPointerException();
        if (other.IsNull)
            throw new ArgumentNullException(nameof(other));
        Intrinsics.Swap(value, other.value);
    }

    /// <inheritdoc/>
    unsafe object? IStrongBox.Value
    {
        get => *value;
        set => *this.value = (T)value!;
    }

    internal unsafe MemoryHandle Pin(nint elementIndex)
        => new(value + elementIndex);

    /// <inheritdoc />
    MemoryHandle IPinnable.Pin(int elementIndex) => Pin(elementIndex);

    /// <inheritdoc />
    void IPinnable.Unpin()
    {
    }

    /// <summary>
    /// Fill memory with zero bytes.
    /// </summary>
    /// <param name="count">Number of elements in the unmanaged array.</param>
    /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
    [CLSCompliant(false)]
    public unsafe void Clear(nuint count)
    {
        if (IsNull)
            ThrowNullPointerException();

        NativeMemory.Clear(value, count);
    }

    /// <summary>
    /// Sets value at the address represented by this pointer to the default value of <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
    public void Clear() => Value = default;

    /// <summary>
    /// Copies a block of memory identifier by this pointer to the specified location.
    /// </summary>
    /// <param name="destination">The destination memory block.</param>
    /// <exception cref="ArgumentNullException">Destination pointer is zero.</exception>
    public unsafe void CopyTo(Span<T> destination)
    {
        if (IsNull)
            ThrowNullPointerException();

        Intrinsics.Copy(in value[0], out MemoryMarshal.GetReference(destination), (nuint)destination.Length);
    }

    /// <summary>
    /// Copies block of memory from the source address to the destination address.
    /// </summary>
    /// <param name="destination">Destination address.</param>
    /// <param name="count">The number of elements to be copied.</param>
    /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
    /// <exception cref="ArgumentNullException">Destination pointer is zero.</exception>
    [CLSCompliant(false)]
    public void CopyTo(Pointer<T> destination, nint count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        CopyTo(destination, (nuint)count);
    }

    /// <summary>
    /// Copies block of memory from the source address to the destination address.
    /// </summary>
    /// <param name="destination">Destination address.</param>
    /// <param name="count">The number of elements to be copied.</param>
    /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
    /// <exception cref="ArgumentNullException">Destination pointer is zero.</exception>
    [CLSCompliant(false)]
    public unsafe void CopyTo(Pointer<T> destination, nuint count)
    {
        if (IsNull)
            throw new NullPointerException(ExceptionMessages.NullSource);
        if (destination.IsNull)
            throw new ArgumentNullException(nameof(destination), ExceptionMessages.NullDestination);

        Intrinsics.Copy(in value[0], out destination.value[0], count);
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
            ThrowNullPointerException();
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (!destination.CanWrite)
            throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));
        if (count > 0)
            WriteTo((byte*)value, checked(count * sizeof(T)), destination);

        static void WriteTo(byte* source, long length, Stream destination)
        {
            for (int count; length > 0; length -= count, source += count)
            {
                destination.Write(new ReadOnlySpan<byte>(source, count = int.CreateSaturating(length)));
            }
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
    public ValueTask WriteToAsync(Stream destination, long count, CancellationToken token = default)
    {
        if (IsNull)
            return ValueTask.FromException(new NullPointerException());

        if (!destination.CanWrite)
            return ValueTask.FromException(new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination)));

        unsafe
        {
            return count is 0L ? ValueTask.CompletedTask : WriteToAsync(Address, checked(count * sizeof(T)), destination, token);
        }

        static async ValueTask WriteToAsync(nint source, long length, Stream destination, CancellationToken token)
        {
            for (int count; length > 0; length -= count, source += count)
            {
                using var manager = new MemorySource(source, count = int.CreateSaturating(length));
                await destination.WriteAsync(manager.Memory, token).ConfigureAwait(false);
            }
        }
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
            ThrowNullPointerException();
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (!source.CanRead)
            throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));

        return count is 0L ? 0L : ReadFrom(source, (byte*)value, checked(sizeof(T) * count));

        static long ReadFrom(Stream source, byte* destination, long length)
        {
            var total = 0L;
            for (int bytesRead; length > 0L; total += bytesRead, length -= bytesRead)
            {
                if ((bytesRead = source.Read(new Span<byte>(&destination[total], int.CreateSaturating(length)))) is 0)
                    break;
            }

            return total;
        }
    }

    /// <summary>
    /// Copies bytes from the given stream to the memory location identified by this pointer asynchronously.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of copied elements.</returns>
    /// <exception cref="NullPointerException">This pointer is zero.</exception>
    /// <exception cref="ArgumentException">The stream is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<long> ReadFromAsync(Stream source, long count, CancellationToken token = default)
    {
        ValueTask<long> result;

        if (count < 0L)
        {
            result = ValueTask.FromException<long>(new ArgumentOutOfRangeException(nameof(count)));
        }
        else if (IsNull)
        {
            result = ValueTask.FromException<long>(new NullPointerException());
        }
        else if (!source.CanRead)
        {
            result = ValueTask.FromException<long>(new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source)));
        }
        else if (count is 0L)
        {
            result = new(0L);
        }
        else
        {
            unsafe
            {
                result = ReadFromStreamAsync(source, Address, checked(sizeof(T) * count), token);
            }
        }

        return result;

        static async ValueTask<long> ReadFromStreamAsync(Stream source, IntPtr destination, long length, CancellationToken token)
        {
            var total = 0L;
            for (int bytesRead; length > 0L; length -= bytesRead, destination += bytesRead, total += bytesRead)
            {
                using var manager = new MemorySource(destination, int.CreateSaturating(length));
                if ((bytesRead = await source.ReadAsync(manager.Memory, token).ConfigureAwait(false)) is 0)
                    break;
            }

            return total;
        }
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
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Stream AsStream(long count, FileAccess access = FileAccess.ReadWrite)
    {
        if (count < 0L)
            throw new ArgumentOutOfRangeException(nameof(count));

        if (IsNull)
            return Stream.Null;

        count = checked(count * sizeof(T));
        return new UnmanagedMemoryStream((byte*)value, count, count, access);
    }

    /// <summary>
    /// Copies the block of memory referenced by this pointer
    /// into managed heap as array of bytes.
    /// </summary>
    /// <param name="length">Number of elements to copy.</param>
    /// <returns>A copy of memory block in the form of byte array.</returns>
    [CLSCompliant(false)]
    public unsafe byte[] ToByteArray(nuint length)
    {
        byte[] result;

        if (IsNull || length is 0U)
        {
            result = [];
        }
        else
        {
            length = checked((nuint)sizeof(T) * length);
            result = length <= (nuint)Array.MaxLength ? GC.AllocateUninitializedArray<byte>((int)length, pinned: true) : new byte[length];
            Intrinsics.Copy(in Unsafe.AsRef<byte>(value), out MemoryMarshal.GetArrayDataReference(result), length);
        }

        return result;
    }

    /// <summary>
    /// Copies the block of memory referenced by this pointer
    /// into managed heap as a pinned array.
    /// </summary>
    /// <param name="length">The length of the memory block to be copied.</param>
    /// <returns>The array containing elements from the memory block referenced by this pointer.</returns>
    [CLSCompliant(false)]
    public unsafe T[] ToArray(nuint length)
    {
        T[] result;

        if (IsNull || length is 0)
        {
            result = [];
        }
        else
        {
            result = length <= (nuint)Array.MaxLength ? GC.AllocateUninitializedArray<T>((int)length, pinned: true) : new T[length];
            Intrinsics.Copy(in value[0], out MemoryMarshal.GetArrayDataReference(result), length);
        }

        return result;
    }

    /// <summary>
    /// Gets pointer address.
    /// </summary>
    public unsafe nint Address
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (nint)value;
    }

    /// <summary>
    /// Indicates that this pointer is <see langword="null"/>.
    /// </summary>
    public unsafe bool IsNull
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => value is null;
    }

    /// <summary>
    /// Reinterprets pointer type.
    /// </summary>
    /// <typeparam name="TOther">A new pointer type.</typeparam>
    /// <returns>Reinterpreted pointer type.</returns>
    /// <exception cref="GenericArgumentException{U}">Type <typeparamref name="TOther"/> should be the same size or less than type <typeparamref name="T"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Pointer<TOther> As<TOther>()
        where TOther : unmanaged
        => sizeof(T) >= sizeof(TOther) ? new Pointer<TOther>(Address) : throw new GenericArgumentException<TOther>(ExceptionMessages.WrongTargetTypeSize, nameof(TOther));

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
                ThrowNullPointerException();

            return ref value[0];
        }
    }

    /// <summary>
    /// Dereferences this pointer.
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
    [CLSCompliant(false)]
    public T Get(nuint index) => this[index];

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
    [CLSCompliant(false)]
    public void Set(T value, nuint index) => this[index] = value;

    /// <summary>
    /// Dereferences this pointer, assuming that this pointer is unaligned.
    /// </summary>
    /// <returns>The value stored in the memory.</returns>
    /// <exception cref="NullPointerException">The pointer is zero.</exception>
    public unsafe T GetUnaligned()
    {
        if (IsNull)
            ThrowNullPointerException();

        return Unsafe.ReadUnaligned<T>(value);
    }

    /// <summary>
    /// Gets the value stored in the memory at the specified position, assuming
    /// that this pointer is unaligned.
    /// </summary>
    /// <param name="index">The index of the element.</param>
    /// <returns>The value stored in the memory at the specified position.</returns>
    /// <exception cref="NullPointerException">The pointer is zero.</exception>
    [CLSCompliant(false)]
    public unsafe T GetUnaligned(nuint index)
    {
        if (IsNull)
            ThrowNullPointerException();

        return Unsafe.ReadUnaligned<T>(value + index);
    }

    /// <summary>
    /// Sets the value stored in the memory identified by this pointer, assuming
    /// that this pointer is unaligned.
    /// </summary>
    /// <param name="value">The value to be stored in the memory.</param>
    /// <exception cref="NullPointerException">The pointer is 0.</exception>
    public unsafe void SetUnaligned(T value)
    {
        if (IsNull)
            ThrowNullPointerException();

        Unsafe.WriteUnaligned(this.value, value);
    }

    /// <summary>
    /// Sets the value at the specified position in the memory, assuming
    /// that this pointer is unaligned.
    /// </summary>
    /// <param name="value">The value to be stored in the memory.</param>
    /// <param name="index">The index of the element to modify.</param>
    /// <exception cref="NullPointerException">The pointer is 0.</exception>
    [CLSCompliant(false)]
    public unsafe void SetUnaligned(T value, nuint index)
    {
        if (IsNull)
            ThrowNullPointerException();

        Unsafe.WriteUnaligned(this.value + index, value);
    }

    /// <summary>
    /// Gets enumerator over raw memory.
    /// </summary>
    /// <param name="length">A number of elements to iterate.</param>
    /// <returns>Iterator object.</returns>
    [CLSCompliant(false)]
    public unsafe Enumerator GetEnumerator(nuint length) => IsNull ? default : new Enumerator(value, length);

    /// <summary>
    /// Bitwise comparison of two memory blocks.
    /// </summary>
    /// <param name="other">The pointer identifies block of memory to be compared.</param>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by both pointers.</param>
    /// <returns>Comparison result which has the semantics as return type of <see cref="IComparable.CompareTo(object)"/>.</returns>
    [CLSCompliant(false)]
    public unsafe int BitwiseCompare(Pointer<T> other, nuint count)
    {
        if (value == other.value)
            return 0;
        if (IsNull)
            ThrowNullPointerException();
        if (other.IsNull)
            throw new ArgumentNullException(nameof(other));

        return Intrinsics.Compare(value, other, checked(count * (nuint)sizeof(T)));
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
        => pointer + (nint)offset;

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
    public static unsafe Pointer<T> operator checked +(Pointer<T> pointer, int offset)
        => checked(pointer + (nint)offset);

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
    public static unsafe Pointer<T> operator +(Pointer<T> pointer, nint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(pointer.value + offset);
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
    public static unsafe Pointer<T> operator checked +(Pointer<T> pointer, nint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(checked(pointer.value + offset));
    }

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
        => pointer - (nint)offset;

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
    public static unsafe Pointer<T> operator checked -(Pointer<T> pointer, int offset)
        => checked(pointer - (nint)offset);

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
    public static unsafe Pointer<T> operator -(Pointer<T> pointer, nint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(pointer.value - offset);
    }

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
    public static unsafe Pointer<T> operator checked -(Pointer<T> pointer, nint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(checked(pointer.value - offset));
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
    public static unsafe Pointer<T> operator +(Pointer<T> pointer, long offset)
        => pointer + (nint)offset;

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
    public static unsafe Pointer<T> operator checked +(Pointer<T> pointer, long offset)
        => checked(pointer + (nint)offset);

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
        => pointer - (nint)offset;

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
    public static unsafe Pointer<T> operator checked -(Pointer<T> pointer, long offset)
        => checked(pointer - (nint)offset);

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
    public static unsafe Pointer<T> operator +(Pointer<T> pointer, nuint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(pointer.value + offset);
    }

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
    public static unsafe Pointer<T> operator -(Pointer<T> pointer, nuint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(pointer.value - offset);
    }

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
    public static unsafe Pointer<T> operator checked -(Pointer<T> pointer, nuint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(checked(pointer.value - offset));
    }

    /// <summary>
    /// Increments this pointer by 1 element of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="pointer">The pointer to add the offset to.</param>
    /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
    public static Pointer<T> operator ++(Pointer<T> pointer) => pointer + (nuint)1;

    /// <summary>
    /// Increments this pointer by 1 element of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="pointer">The pointer to add the offset to.</param>
    /// <returns>A new pointer that reflects the addition of offset to pointer.</returns>
    public static Pointer<T> operator checked ++(Pointer<T> pointer) => checked(pointer + (nuint)1);

    /// <summary>
    /// Decrements this pointer by 1 element of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="pointer">The pointer to subtract the offset from.</param>
    /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
    public static Pointer<T> operator --(Pointer<T> pointer) => pointer - (nuint)1;

    /// <summary>
    /// Decrements this pointer by 1 element of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="pointer">The pointer to subtract the offset from.</param>
    /// <returns>A new pointer that reflects the subtraction of offset from pointer.</returns>
    public static Pointer<T> operator checked --(Pointer<T> pointer) => checked(pointer - (nuint)1);

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
    /// Determines whether the address of the first pointer is greater
    /// than the address of the second pointer.
    /// </summary>
    /// <param name="first">The first pointer to compare.</param>
    /// <param name="second">The second pointer to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is greater than <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool operator >(Pointer<T> first, Pointer<T> second)
        => first.value > second.value;

    /// <summary>
    /// Determines whether the address of the first pointer is less
    /// than the address of the second pointer.
    /// </summary>
    /// <param name="first">The first pointer to compare.</param>
    /// <param name="second">The second pointer to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is less than <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool operator <(Pointer<T> first, Pointer<T> second)
        => first.value < second.value;

    /// <summary>
    /// Determines whether the address of the first pointer is greater
    /// than or equal to the address of the second pointer.
    /// </summary>
    /// <param name="first">The first pointer to compare.</param>
    /// <param name="second">The second pointer to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is greater than or equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool operator >=(Pointer<T> first, Pointer<T> second)
        => first.value >= second.value;

    /// <summary>
    /// Determines whether the address of the first pointer is less
    /// than or equal to the address of the second pointer.
    /// </summary>
    /// <param name="first">The first pointer to compare.</param>
    /// <param name="second">The second pointer to compare.</param>
    /// <returns><see langword="true"/> if <paramref name="first"/> is less than or equal to <paramref name="second"/>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe bool operator <=(Pointer<T> first, Pointer<T> second)
        => first.value <= second.value;

    /// <summary>
    /// Converts non CLS-compliant pointer into its CLS-compliant representation.
    /// </summary>
    /// <param name="value">The pointer value.</param>
    [CLSCompliant(false)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe implicit operator Pointer<T>(T* value) => new(value);

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
    public static implicit operator nint(Pointer<T> ptr) => ptr.Address;

    /// <inheritdoc/>
    IntPtr ISupplier<IntPtr>.Invoke() => Address;

    /// <summary>
    /// Obtains pointer value (address) as <see cref="UIntPtr"/>.
    /// </summary>
    /// <param name="ptr">The pointer to be converted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe implicit operator nuint(Pointer<T> ptr) => (nuint)ptr.value;

    /// <inheritdoc/>
    unsafe UIntPtr ISupplier<UIntPtr>.Invoke() => (nuint)value;

    /// <summary>
    /// Converts this pointer the memory owner.
    /// </summary>
    /// <param name="length">The number of elements in the memory.</param>
    /// <returns>The instance of memory owner.</returns>
    public unsafe IMemoryOwner<T> ToMemoryOwner(int length)
    {
        nint address;
        if (IsNull)
        {
            length = 0;
            address = 0;
        }
        else
        {
            address = (nint)value;
        }

        return new Buffers.UnmanagedMemory<T>(address, length);
    }

    /// <summary>
    /// Obtains pointer to the memory represented by given memory handle.
    /// </summary>
    /// <param name="handle">The memory handle.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe explicit operator Pointer<T>(in MemoryHandle handle) => new((nint)handle.Pointer);

    /// <summary>
    /// Checks whether this pointer is not zero.
    /// </summary>
    /// <param name="ptr">The pointer to check.</param>
    /// <returns><see langword="true"/>, if this pointer is not zero; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator true(Pointer<T> ptr) => ptr.IsNull is false;

    /// <summary>
    /// Checks whether this pointer is zero.
    /// </summary>
    /// <param name="ptr">The pointer to check.</param>
    /// <returns><see langword="true"/>, if this pointer is zero; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator false(Pointer<T> ptr) => ptr.IsNull;

    /// <inheritdoc/>
    bool IEquatable<Pointer<T>>.Equals(Pointer<T> other) => Equals(other);

    /// <summary>
    /// Indicates that this pointer represents the same memory location as other pointer.
    /// </summary>
    /// <typeparam name="TOther">The type of the another pointer.</typeparam>
    /// <param name="other">The pointer to be compared.</param>
    /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe bool Equals<TOther>(Pointer<TOther> other)
        where TOther : unmanaged => value == other.value;

    /// <inheritdoc />
    unsafe int IComparable<Pointer<T>>.CompareTo(Pointer<T> other)
    {
        if (value < other.value)
            return -1;

        if (value > other.value)
            return 1;

        return 0;
    }

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
    public override unsafe int GetHashCode() => Intrinsics.PointerHashCode(value);

    /// <summary>
    /// Indicates that this pointer represents the same memory location as other pointer.
    /// </summary>
    /// <param name="other">The object of type <see cref="Pointer{T}"/> to be compared.</param>
    /// <returns><see langword="true"/>, if this pointer represents the same memory location as other pointer; otherwise, <see langword="false"/>.</returns>
    public override unsafe bool Equals([NotNullWhen(true)] object? other) => other switch
    {
        Pointer<T> ptr => Equals(ptr),
        Pointer ptr => value == Pointer.Unbox(ptr),
        _ => false
    };

    /// <inheritdoc />
    string IFormattable.ToString(string? format, IFormatProvider? provider)
        => Address.ToString(format, provider);

    /// <inheritdoc />
    bool ISpanFormattable.TryFormat(Span<char> destination, out int charsWritten, ReadOnlySpan<char> format, IFormatProvider? provider)
        => Address.TryFormat(destination, out charsWritten, format, provider);

    /// <summary>
    /// Returns hexadecimal address represented by this pointer.
    /// </summary>
    /// <returns>The hexadecimal value of this pointer.</returns>
    public override string ToString() => Address.ToString("X");
}