using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T> :
    IComparisonOperators<Pointer<T>, Pointer<T>, bool>,
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
{
    /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity"/>
    static nint IAdditiveIdentity<Pointer<T>, nint>.AdditiveIdentity => 0;

    /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity"/>
    static nuint IAdditiveIdentity<Pointer<T>, nuint>.AdditiveIdentity => 0U;
    
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
    public static Pointer<T> operator checked +(Pointer<T> pointer, int offset)
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

        return new(checked((nint)pointer.value + offset));
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
    public static Pointer<T> operator -(Pointer<T> pointer, int offset)
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
    public static Pointer<T> operator checked -(Pointer<T> pointer, int offset)
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
    public static Pointer<T> operator checked -(Pointer<T> pointer, nint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(checked(pointer.Address - offset));
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
    public static Pointer<T> operator +(Pointer<T> pointer, long offset)
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
    public static Pointer<T> operator checked +(Pointer<T> pointer, long offset)
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
    public static Pointer<T> operator -(Pointer<T> pointer, long offset)
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
    public static Pointer<T> operator checked -(Pointer<T> pointer, long offset)
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
    public static unsafe Pointer<T> operator checked +(Pointer<T> pointer, nuint offset)
    {
        if (pointer.IsNull)
            ThrowNullPointerException();

        return new(checked((nuint)pointer.value + offset));
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

        return new(checked((nuint)pointer.value - offset));
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
    public static Pointer<T> operator checked --(Pointer<T> pointer) => checked(pointer - (nuint)1U);

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
    
    /// <summary>
    /// Obtains pointer value (address) as <see cref="UIntPtr"/>.
    /// </summary>
    /// <param name="ptr">The pointer to be converted.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [CLSCompliant(false)]
    public static unsafe implicit operator nuint(Pointer<T> ptr) => (nuint)ptr.value;
    
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
    
    /// <summary>
    /// Gets the reference to the unmanaged memory.
    /// </summary>
    /// <param name="pointer">The memory block.</param>
    /// <returns>The reference to the unmanaged memory.</returns>
    public static unsafe implicit operator ValueReference<T>(Pointer<T> pointer)
    {
        T* ptr = pointer;
        return ptr is not null ? new(ref *ptr) : default;
    }
}