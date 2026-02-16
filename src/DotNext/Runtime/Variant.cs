using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Reflection;

namespace DotNext.Runtime;

using CompilerServices;

/// <summary>
/// Represents the variant value on the stack.
/// </summary>
/// <remarks>
/// This type is a counterpart of <see cref="TypedReference"/>.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct Variant : IEquatable<Variant>
{
    private readonly ref byte location;
    private readonly Type targetType;

    private Variant(ref byte location, Type type, bool mutable)
    {
        this.location = ref location;
        targetType = mutable ? type.MakeByRefType() : type;
    }

    /// <summary>
    /// Gets empty value which <see cref="IsEmpty"/> is <see langword="true"/>.
    /// </summary>
    public static Variant Empty => default;

    /// <summary>
    /// Gets a value indicating that this value is empty.
    /// </summary>
    public bool IsEmpty => Unsafe.IsNullRef(in location);

    /// <summary>
    /// Gets the type of the value.
    /// </summary>
    public Type TargetType
    {
        get
        {
            return Infer(targetType);
            
            static Type Infer(Type? targetType) => targetType switch
            {
                null => typeof(void),
                { IsByRef: true } => targetType.GetElementType()!,
                _ => targetType,
            };
        }
    }

    /// <summary>
    /// Gets a value indicating that the underlying value can be mutated.
    /// </summary>
    public bool IsMutable => targetType is { IsByRef: true };

    private static Variant Create<T>(ref readonly T location, bool mutable = false)
        where T : allows ref struct
        => new(ref Unsafe.InToRef<T, byte>(in location), typeof(T), mutable);

    /// <summary>
    /// Creates immutable value and stores its type.
    /// </summary>
    /// <param name="location">The location of the value.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The untyped value.</returns>
    public static Variant Immutable<T>(ref readonly T location)
        where T : allows ref struct
        => Create(in location, mutable: false);

    /// <summary>
    /// Creates mutable value and stores its type.
    /// </summary>
    /// <param name="location">The location of the value.</param>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <returns>The untyped value.</returns>
    public static Variant Mutable<T>(ref T location)
        where T : allows ref struct
        => Create(ref location, mutable: true);

    /// <summary>
    /// Creates mutable boxed value and stores its type.
    /// </summary>
    /// <param name="boxedValue">The boxed value.</param>
    /// <returns>The boxed value.</returns>
    public static Variant Mutable(ValueType boxedValue)
    {
        ArgumentNullException.ThrowIfNull(boxedValue);

        return new(ref Unsafe.GetRawData(boxedValue), boxedValue.GetType(), mutable: true);
    }

    /// <summary>
    /// Gets the read-only reference to the underlying value.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <returns>A location of the value.</returns>
    /// <exception cref="InvalidCastException">The underlying value cannot be converted to type <typeparamref name="T"/>.</exception>
    public ref readonly T Immutable<T>()
        where T : allows ref struct
    {
        if (!CheckType(TargetType, typeof(T)))
            InvalidCastException.Throw();

        return ref Unsafe.As<byte, T>(ref location);

        static bool CheckType(Type? expected, Type actual) => expected is { IsByRef: true }
            ? expected.GetElementType() == actual
            : expected == actual;
    }

    /// <summary>
    /// Tries to get the mutable reference to the underlying value.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <returns>A location of the value.</returns>
    /// <exception cref="InvalidCastException">The underlying value cannot be converted to type <typeparamref name="T"/>;
    /// or it is not mutable.</exception>
    public ref T Mutable<T>()
        where T : allows ref struct
    {
        if (!CheckMutableType(targetType, typeof(T)))
            InvalidCastException.Throw();

        return ref Unsafe.As<byte, T>(ref location);

        static bool CheckMutableType(Type? expected, Type actual)
            => expected is { IsByRef: true } && expected.GetElementType() == actual;
    }

    /// <summary>
    /// Tries to extract the underlying value.
    /// </summary>
    /// <returns>The underlying value.</returns>
    /// <exception cref="NotSupportedException">The underlying value is by-ref like struct and cannot be boxed.</exception>
    public object? ToObject() => TargetType switch
    {
        { IsVoid: true } => null,
        { IsByRefLike: true } => throw new NotSupportedException(),
        { IsValueType: true } vt => RuntimeHelpers.Box(ref location, vt.TypeHandle),
        _ => Unsafe.As<byte, object>(ref location)
    };

    /// <summary>
    /// Checks whether this container references the same value and type as <paramref name="other"/>.
    /// </summary>
    /// <param name="other">The value to compare.</param>
    /// <returns><see langword="true"/>, if this container references the same value and type as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
    public bool Equals(scoped Variant other)
        => TargetType == other.TargetType && Unsafe.AreSame(in location, in other.location);
    
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
    /// Determines whether the two containers reference the same value and type.
    /// </summary>
    /// <param name="x">The first container to compare.</param>
    /// <param name="y">The second container to compare.</param>
    /// <returns><see langword="true"/> if both containers reference the same value and type; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Variant x, Variant y)
        => x.Equals(y);

    /// <summary>
    /// Determines whether the two containers reference the different values and types.
    /// </summary>
    /// <param name="x">The first container to compare.</param>
    /// <param name="y">The second container to compare.</param>
    /// <returns><see langword="true"/> if both containers reference the different values and types; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Variant x, Variant y)
        => !x.Equals(y);
}