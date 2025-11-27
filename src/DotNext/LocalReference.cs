using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext;

/// <summary>
/// Represents a typed reference.
/// </summary>
/// <typeparam name="T">The type of the referenced value.</typeparam>
public interface ITypedReference<T>
    where T : allows ref struct
{
    /// <summary>
    /// Gets a reference to the value.
    /// </summary>
    ref readonly T Value { get; }
    
    /// <summary>
    /// Gets a value indicating that this reference is empty.
    /// </summary>
    bool IsEmpty { get; }
}

/// <summary>
/// Represents a typed reference that can be passed as a generic argument.
/// </summary>
/// <param name="location">The location of the value.</param>
/// <typeparam name="T">The type of the referenced value.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct LocalReference<T>(ref T location) : ITypedReference<T>, IEquatable<LocalReference<T>>
    where T : allows ref struct
{
    private readonly ref byte location = ref Unsafe.As<T, byte>(ref location);

    /// <summary>
    /// The referenced value.
    /// </summary>
    public ref T Value => ref Unsafe.As<byte, T>(ref location);
    
    /// <summary>
    /// Gets a value indicating that this reference is empty.
    /// </summary>
    public bool IsEmpty => Unsafe.IsNullRef(in Value);

    /// <inheritdoc/>
    ref readonly T ITypedReference<T>.Value => ref Value;

    /// <inheritdoc/>
    public bool Equals(LocalReference<T> other) => Unsafe.AreSame(ref Value, ref other.Value);

    /// <inheritdoc/>
    [DoesNotReturn]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals([NotNullWhen(true)] object? obj)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
        => throw new NotSupportedException();
    
    /// <summary>
    /// Obtains managed pointer to the referenced value.
    /// </summary>
    /// <returns>The managed pointer to the referenced value.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref T GetPinnableReference() => ref Value;

    /// <summary>
    /// Checks whether the two references point to the same memory location.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references point to the same memory location; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(LocalReference<T> x, LocalReference<T> y)
        => x.Equals(y);

    /// <summary>
    /// Checks whether the two references point to the different memory locations.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references point to the different memory locations; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(LocalReference<T> x, LocalReference<T> y)
        => !x.Equals(y);

    /// <summary>
    /// Converts mutable typed reference to the read-only typed reference.
    /// </summary>
    /// <param name="reference">The reference to convert.</param>
    /// <returns>Read-only view of the same memory location as presented by <paramref name="reference"/>.</returns>
    public static implicit operator ReadOnlyLocalReference<T>(LocalReference<T> reference)
        => new(in reference.Value);
}

/// <summary>
/// Represents read-only typed reference.
/// </summary>
/// <param name="location">The location of the value.</param>
/// <typeparam name="T">The type of the referenced value.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct ReadOnlyLocalReference<T>(ref readonly T location) : ITypedReference<T>, IEquatable<ReadOnlyLocalReference<T>>
    where T : allows ref struct
{
    private readonly ref byte location = ref Unsafe.As<T, byte>(ref Unsafe.AsRef(in location));

    /// <summary>
    /// The referenced value.
    /// </summary>
    public ref readonly T Value => ref Unsafe.As<byte, T>(ref location);

    /// <summary>
    /// Gets a value indicating that this reference is empty.
    /// </summary>
    public bool IsEmpty => Unsafe.IsNullRef(in Value);

    /// <inheritdoc/>
    ref readonly T ITypedReference<T>.Value => ref Value;

    /// <inheritdoc/>
    public bool Equals(ReadOnlyLocalReference<T> other) => Unsafe.AreSame(in Value, in other.Value);

    /// <inheritdoc/>
    [DoesNotReturn]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override bool Equals([NotNullWhen(true)] object? obj)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    [DoesNotReturn]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public override int GetHashCode()
        => throw new NotSupportedException();
    
    /// <summary>
    /// Obtains managed pointer to the referenced value.
    /// </summary>
    /// <returns>The managed pointer to the referenced value.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref readonly T GetPinnableReference() => ref Value;

    /// <summary>
    /// Checks whether the two references point to the same memory location.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references point to the same memory location; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ReadOnlyLocalReference<T> x, ReadOnlyLocalReference<T> y)
        => x.Equals(y);

    /// <summary>
    /// Checks whether the two references point to the different memory locations.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references point to the different memory locations; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ReadOnlyLocalReference<T> x, ReadOnlyLocalReference<T> y)
        => !x.Equals(y);
}