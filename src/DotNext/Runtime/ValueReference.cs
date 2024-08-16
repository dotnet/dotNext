using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Reflection;
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
[EditorBrowsable(EditorBrowsableState.Advanced)]
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
    /// Creates a reference to a static field.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="T"/> is a value type then your static field MUST be marked
    /// with <see cref="FixedAddressValueTypeAttribute"/>. Otherwise, the behavior is unpredictable.
    /// Correctness of this constructor is based on the fact that static fields are stored
    /// as elements of <see cref="object"/> array allocated by the runtime in the Pinned Object Heap.
    /// It means that the address of the field cannot be changed by GC.
    /// </remarks>
    /// <param name="staticFieldRef">A reference to the static field.</param>
    /// <seealso href="https://devblogs.microsoft.com/dotnet/internals-of-the-poh/">Internals of the POH</seealso>
    [CLSCompliant(false)]
    public ValueReference(ref T staticFieldRef)
        : this(Sentinel.Instance, ref staticFieldRef)
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
/// Represents an immutable reference to the field.
/// </summary>
/// <param name="owner">An object that owns the field.</param>
/// <param name="fieldRef">The reference to the field.</param>
/// <typeparam name="T">The type of the field.</typeparam>
[StructLayout(LayoutKind.Auto)]
[EditorBrowsable(EditorBrowsableState.Advanced)]
public readonly struct ReadOnlyValueReference<T>(object owner, ref readonly T fieldRef) :
    IEquatable<ReadOnlyValueReference<T>>,
    IEqualityOperators<ReadOnlyValueReference<T>, ReadOnlyValueReference<T>, bool>
{
    private readonly nint offset = RawData.GetOffset(owner, in fieldRef);
    
    /// <summary>
    /// Creates a reference to a static field.
    /// </summary>
    /// <remarks>
    /// If <typeparamref name="T"/> is a value type then your static field MUST be marked
    /// with <see cref="FixedAddressValueTypeAttribute"/>. Otherwise, the behavior is unpredictable.
    /// Correctness of this constructor is based on the fact that static fields are stored
    /// as elements of <see cref="object"/> array allocated by the runtime in the Pinned Object Heap.
    /// It means that the address of the field cannot be changed by GC.
    /// </remarks>
    /// <param name="staticFieldRef">A reference to the static field.</param>
    /// <seealso href="https://devblogs.microsoft.com/dotnet/internals-of-the-poh/">Internals of the POH</seealso>
    public ReadOnlyValueReference(ref readonly T staticFieldRef)
        : this(Sentinel.Instance, in staticFieldRef)
    {
    }
    
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
    // TODO: Replace with public counterpart in future
    private static readonly Func<object, nuint>? GetRawObjectDataSize;

    [DynamicDependency(DynamicallyAccessedMemberTypes.NonPublicMethods, typeof(RuntimeHelpers))]
    static RawData()
    {
        const BindingFlags flags = BindingFlags.DeclaredOnly | BindingFlags.NonPublic | BindingFlags.Static;
        GetRawObjectDataSize = typeof(RuntimeHelpers)
            .GetMethod(nameof(GetRawObjectDataSize), flags, [typeof(object)])
            ?.CreateDelegate<Func<object, nuint>>();
    }
    
    private byte data;

    private RawData() => throw new NotImplementedException();

    internal static nint GetOffset<T>(object owner, ref readonly T field, [CallerArgumentExpression(nameof(field))] string? paramName = null)
    {
        ref var rawData = ref Unsafe.As<RawData>(owner).data;
        var offset = Unsafe.ByteOffset(in rawData, in Intrinsics.ChangeType<T, byte>(in field));

        // Ensure that the reference is an interior pointer to the field inside the object
        if (GetRawObjectDataSize is not null && owner != Sentinel.Instance && (nuint)offset > GetRawObjectDataSize(owner))
            throw new ArgumentOutOfRangeException(paramName);

        return offset;
    }

    internal static ref T GetObjectData<T>(object owner, nint offset)
    {
        ref var rawData = ref Unsafe.As<RawData>(owner).data;
        return ref Unsafe.As<byte, T>(ref Unsafe.Add(ref rawData, offset));
    }
}