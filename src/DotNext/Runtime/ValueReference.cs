using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

/// <summary>
/// Represents a mutable reference to the field.
/// </summary>
/// <param name="owner">An object that owns the field.</param>
/// <param name="fieldRef">The reference to the field.</param>
/// <typeparam name="T">The type of the field.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct ValueReference<T>(object owner, ref T fieldRef) :
    IEquatable<ValueReference<T>>,
    IEqualityOperators<ValueReference<T>, ValueReference<T>, bool>
{
    private readonly nint offset = RawData.GetOffset(owner, in fieldRef);

    /// <summary>
    /// Creates a reference to an array element.
    /// </summary>
    /// <param name="array">The array.</param>
    /// <param name="index">The index of the array element.</param>
    public ValueReference(T[] array, int index)
        : this(array, ref array[index])
    {
    }

    private ValueReference(StrongBox<T> box)
        : this(box, ref box.Value!)
    {
    }

    /// <summary>
    /// Creates a reference to the anonymous value.
    /// </summary>
    /// <param name="value">The anonymous value.</param>
    public ValueReference(T value)
        : this(new StrongBox<T> { Value = value })
    {
    }

    /// <summary>
    /// Gets a value indicating that is reference is empty.
    /// </summary>
    public bool IsEmpty => owner is null;

    /// <summary>
    /// Gets a reference to the field.
    /// </summary>
    public ref T Value => ref RawData.GetObjectData<T>(owner, offset);

    private bool SameObject(object? other) => ReferenceEquals(owner, other);

    /// <inheritdoc/>
    public override string? ToString()
        => owner is not null ? RawData.GetObjectData<T>(owner, offset)?.ToString() : null;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is ValueReference<T> otherRef && Equals(otherRef);

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(owner) ^ offset.GetHashCode();

    /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
    public bool Equals(ValueReference<T> reference)
        => reference.SameObject(owner) && offset == reference.offset;

    /// <summary>
    /// Determines whether the two references point to the same field.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ValueReference<T> x, ValueReference<T> y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two references point to the different fields.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ValueReference<T> x, ValueReference<T> y)
        => x.Equals(y) is false;

    /// <summary>
    /// Converts mutable field reference to immutable field reference.
    /// </summary>
    /// <param name="reference">The reference to convert.</param>
    /// <returns>The immutable field reference.</returns>
    public static implicit operator ReadOnlyValueReference<T>(ValueReference<T> reference)
        => Unsafe.BitCast<ValueReference<T>, ReadOnlyValueReference<T>>(reference);
}

/// <summary>
/// Represents a mutable reference to the field.
/// </summary>
/// <param name="owner">An object that owns the field.</param>
/// <param name="fieldRef">The reference to the field.</param>
/// <typeparam name="T">The type of the field.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct ReadOnlyValueReference<T>(object owner, ref readonly T fieldRef) :
    IEquatable<ReadOnlyValueReference<T>>,
    IEqualityOperators<ReadOnlyValueReference<T>, ReadOnlyValueReference<T>, bool>
{
    private readonly nint offset = RawData.GetOffset(owner, in fieldRef);
    
    /// <summary>
    /// Gets a value indicating that is reference is empty.
    /// </summary>
    public bool IsEmpty => owner is null;

    /// <summary>
    /// Gets a reference to the field.
    /// </summary>
    public ref readonly T Value => ref RawData.GetObjectData<T>(owner, offset);

    private bool SameObject(object? other) => ReferenceEquals(owner, other);

    /// <inheritdoc/>
    public override string? ToString()
        => owner is not null ? RawData.GetObjectData<T>(owner, offset)?.ToString() : null;

    /// <inheritdoc/>
    public override bool Equals([NotNullWhen(true)] object? other)
        => other is ReadOnlyValueReference<T> otherRef && Equals(otherRef);

    /// <inheritdoc/>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(owner) ^ offset.GetHashCode();

    /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
    public bool Equals(ReadOnlyValueReference<T> reference)
        => reference.SameObject(owner) && offset == reference.offset;

    /// <summary>
    /// Determines whether the two references point to the same field.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(ReadOnlyValueReference<T> x, ReadOnlyValueReference<T> y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two references point to the different fields.
    /// </summary>
    /// <param name="x">The first reference to compare.</param>
    /// <param name="y">The second reference to compare.</param>
    /// <returns><see langword="true"/> if both references are not equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(ReadOnlyValueReference<T> x, ReadOnlyValueReference<T> y)
        => x.Equals(y) is false;
}

[SuppressMessage("Performance", "CA1812", Justification = "Used for reinterpret cast")]
file sealed class RawData
{
    private byte data;

    private RawData() => throw new NotImplementedException();

    internal static nint GetOffset<T>(object owner, ref readonly T field)
    {
        ref var rawData = ref Unsafe.As<RawData>(owner).data;
        return Unsafe.ByteOffset(in rawData, in Intrinsics.ChangeType<T, byte>(in field));
    }

    internal static ref T GetObjectData<T>(object owner, nint offset)
    {
        ref var rawData = ref Unsafe.As<RawData>(owner).data;
        return ref Unsafe.As<byte, T>(ref Unsafe.Add(ref rawData, offset));
    }
}