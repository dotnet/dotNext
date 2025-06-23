using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Pointer = System.Reflection.Pointer;

namespace DotNext.Runtime.InteropServices;

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
public readonly partial struct Pointer<T> :
    IEquatable<Pointer<T>>,
    IComparable<Pointer<T>>,
    IStrongBox,
    ISupplier<nint>,
    ISupplier<nuint>,
    ISpanFormattable
    where T : unmanaged
{
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
    public bool IsAligned => Address % Intrinsics.AlignOf<T>() is 0;

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
        if (sizeof(T) is sizeof(byte))
        {
            NativeMemory.Fill(pointer, count, Unsafe.BitCast<T, byte>(value));
        }
        else
        {
            for (int len; count > 0; count -= (uint)len, pointer += len)
            {
                len = int.CreateSaturating(count);
                new Span<T>(pointer, len).Fill(value);
            }
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

        ArgumentNullException.ThrowIfNull(other.value, nameof(other));

        Intrinsics.Swap(value, other.value);
    }

    /// <inheritdoc/>
    unsafe object? IStrongBox.Value
    {
        get => *value;
        set => *this.value = (T)value!;
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

        var pointer = value;
        if (sizeof(T) is sizeof(byte))
        {
            NativeMemory.Clear(pointer, count);
        }
        else
        {
            for (int len; count > 0; count -= (uint)len, pointer += len)
            {
                len = int.CreateSaturating(count);
                new Span<T>(pointer, len).Clear();
            }
        }
    }

    /// <summary>
    /// Sets value at the address represented by this pointer to the default value of <typeparamref name="T"/>.
    /// </summary>
    /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
    public void Clear()
    {
        if (IsNull)
            ThrowNullPointerException();

        Value = default;
    }

    /// <summary>
    /// Copies a block of memory identifier by this pointer to the specified location.
    /// </summary>
    /// <param name="destination">The destination memory block.</param>
    /// <exception cref="ArgumentNullException">Destination pointer is zero.</exception>
    public unsafe void CopyTo(Span<T> destination)
    {
        if (IsNull)
            ThrowNullPointerException();

        new ReadOnlySpan<T>(value, destination.Length).CopyTo(destination);
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
            ThrowNullPointerException();

        ArgumentNullException.ThrowIfNull(destination.value, nameof(destination));

        if (sizeof(T) is sizeof(byte))
        {
            NativeMemory.Copy(value, destination.value, count);
        }
        else
        {
            Intrinsics.Copy(in *value, out *destination.value, count);
        }
    }

    /// <summary>
    /// Copies the block of memory referenced by this pointer
    /// into managed heap as array of bytes.
    /// </summary>
    /// <param name="length">Number of elements to copy.</param>
    /// <returns>A copy of memory block in the form of byte array.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public unsafe byte[] ToByteArray(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        byte[] result;
        if (IsNull || length is 0)
        {
            result = [];
        }
        else
        {
            length = checked(sizeof(T) * length);
            result = GC.AllocateUninitializedArray<byte>(length, pinned: true);

            Intrinsics.Copy(in Unsafe.AsRef<byte>(value), out MemoryMarshal.GetArrayDataReference(result), (nuint)length);
        }

        return result;
    }

    /// <summary>
    /// Copies the block of memory referenced by this pointer
    /// into managed heap as a pinned array.
    /// </summary>
    /// <param name="length">The length of the memory block to be copied.</param>
    /// <returns>The array containing elements from the memory block referenced by this pointer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="length"/> is negative.</exception>
    public unsafe T[] ToArray(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        T[] result;

        if (IsNull || length is 0)
        {
            result = [];
        }
        else
        {
            result = GC.AllocateUninitializedArray<T>(length, pinned: true);
            Intrinsics.Copy(in value[0], out MemoryMarshal.GetArrayDataReference(result), (nuint)length);
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
        get => ref *value;
    }

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

        ArgumentNullException.ThrowIfNull(other.value, nameof(other));

        return Intrinsics.Compare(value, other, checked(count * (uint)sizeof(T)));
    }

    /// <inheritdoc/>
    nint ISupplier<nint>.Invoke() => Address;

    /// <inheritdoc/>
    unsafe nuint ISupplier<nuint>.Invoke() => (nuint)value;

    /// <summary>
    /// Converts this pointer the memory owner.
    /// </summary>
    /// <param name="length">The number of elements in the memory.</param>
    /// <returns>The instance of memory owner.</returns>
    public unsafe IMemoryOwner<T> ToMemoryOwner(int length)
        => IsNull || length is 0 ? Buffers.UnmanagedMemory<T>.Empty() : new Buffers.UnmanagedMemory<T>(value, length);

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
    int IComparable<Pointer<T>>.CompareTo(Pointer<T> other)
        => Address.CompareTo(other.Address);

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
        nint address => (nint)value == address,
        nuint address => (nuint)value == address,
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