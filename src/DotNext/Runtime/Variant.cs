using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

using CompilerServices;
using Reflection;

/// <summary>
/// Represents the variant value on the stack.
/// </summary>
/// <remarks>
/// This type is a counterpart of <see cref="TypedReference"/>.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct Variant : IEquatable<Variant>
{
    // The lowest bit of the type handle is always zero due to alignment, so it's used as the mutability flag
    private const nint MutableFlag = 1;

    private readonly ref byte location;
    private readonly nint typeInfo; // RuntimeTypeHandle.Value | MutableFlag

    private Variant(ref byte location, RuntimeTypeHandle type, bool mutable)
    {
        this.location = ref location;
        typeInfo = Encode(type.Value, mutable);
    }

    private static nint Encode(nint typeHandle, bool mutable)
    {
        Debug.Assert((typeHandle & MutableFlag) is 0);

        return typeHandle | Unsafe.BitCast<bool, byte>(mutable);
    }

    private nint TypeHandle => typeInfo & ~MutableFlag;

    /// <summary>
    /// Converts <see cref="TypedReference"/> to the variant value.
    /// </summary>
    /// <param name="typedRef">The typed reference to convert.</param>
    [CLSCompliant(false)]
    public Variant(TypedReference typedRef)
        : this(typedRef, mutable: true)
    {
    }

    internal Variant(TypedReference typedRef, bool mutable)
    {
        TypedReferenceAlias.AssertLayout();
#pragma warning disable CS8500
        unsafe
        {
            ref readonly var alias = ref TypedReferenceAlias.Create(&typedRef);
            location = ref alias.Location;
            typeInfo = Encode(alias.TypeHandle, mutable);
        }
#pragma warning restore CS8500
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
        => Type.GetTypeFromHandle(RuntimeTypeHandle.FromIntPtr(TypeHandle)) ?? typeof(void);

    /// <summary>
    /// Gets a value indicating that the underlying value can be mutated.
    /// </summary>
    public bool IsMutable => (typeInfo & MutableFlag) is not 0;

    private static Variant Create<T>(ref readonly T location, bool mutable = false)
        where T : allows ref struct
        => new(ref Unsafe.InToRef<T, byte>(in location), AdvancedHelpers.get_TypeId<T>(), mutable);

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
    /// Creates immutable value.
    /// </summary>
    /// <param name="value">The location of the value.</param>
    /// <returns>The untyped immutable value.</returns>
    public static Variant Immutable(Variant value)
    {
        Unsafe.AsRef(in value.typeInfo) = value.TypeHandle;
        return value;
    }

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

        return new(ref Unsafe.GetRawData(boxedValue),
            boxedValue.GetType().TypeHandle,
            mutable: true);
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
        if (TypeHandle != AdvancedHelpers.get_TypeId<T>().Value)
            InvalidCastException.Throw();

        return ref Unsafe.As<byte, T>(ref location);
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
        // checks both the type and mutability flag
        if (typeInfo != (AdvancedHelpers.get_TypeId<T>().Value | MutableFlag))
            InvalidCastException.Throw();

        return ref Unsafe.As<byte, T>(ref location);
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
        => TypeHandle == other.TypeHandle && Unsafe.AreSame(in location, in other.location);
    
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

    /// <summary>
    /// Converts <see cref="TypedReference"/> to the variant value.
    /// </summary>
    /// <param name="typedRef">The typed reference to convert.</param>
    /// <returns>The variant points to the same location as <paramref name="typedRef"/>.</returns>
    [CLSCompliant(false)]
    public static implicit operator Variant(TypedReference typedRef) => new(typedRef);
}

file readonly ref struct TypedReferenceAlias
{
#pragma warning disable CS0649, CS9265, CS8500
    public readonly ref byte Location;
    public readonly nint TypeHandle;

    public static unsafe ref TypedReferenceAlias Create(TypedReference* typedRef)
        => ref Unsafe.AsRef<TypedReferenceAlias>(typedRef);
    
    [Conditional("DEBUG")]
    [Conditional("PUBLISH_AOT")]
    public static unsafe void AssertLayout()
    {
        byte i = 42;
        var typedRef = __makeref(i);
        ref readonly var alias = ref Create(&typedRef);

        if (sizeof(TypedReference) == sizeof(TypedReferenceAlias)
            && Unsafe.AreSame(in i, in alias.Location)
            && TypedReference.TargetTypeToken(typedRef).Equals(RuntimeTypeHandle.FromIntPtr(alias.TypeHandle)))
        {
            // layout is correct
        }
        else if (Debugger.IsAttached)
        {
            Debugger.Break();
        }
        else
        {
            const string message = "Unexpected TypedReference memory layout";
            var e = new UnreachableException(message);
            ExceptionDispatchInfo.SetCurrentStackTrace(e);
            Environment.FailFast(message, e);
        }
    }
#pragma warning restore CS0649, CS9265, CS8500
}