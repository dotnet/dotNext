using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime;

/// <summary>
/// Represents a typed representation of the boxed value type.
/// </summary>
/// <remarks>
/// <see cref="object.Equals(object?)"/>, <see cref="object.GetHashCode"/> and <see cref="object.ToString"/>
/// implementations match to the real implementations of the appropriate value type.
/// This type is completely compatible with runtime representation of the boxed value. For instance,
/// the following code is correct:
/// <code>
/// static int Unbox(object obj) =&gt; (int)obj;
/// BoxedValue&lt;int&gt; boxed = BoxedValue&lt;int&gt;.Box(42);
/// int value = Unbox(boxed);
/// </code>
/// Additionally, it means that <see cref="object.GetType"/> returns type information for <typeparamref name="T"/> type,
/// not for <see cref="BoxedValue{T}"/>.
/// </remarks>
/// <typeparam name="T">The value type.</typeparam>
public abstract class BoxedValue<T> // do not add any interfaces or base types
    where T : struct
{
    internal T Value;

    /// <summary>
    /// Converts untyped reference to a boxed value into a typed reference.
    /// </summary>
    /// <remarks>
    /// This method doesn't allocate memory.
    /// </remarks>
    /// <param name="boxedValue">The boxed value of type <typeparamref name="T"/>.</param>
    /// <returns>The typed reference to a boxed value of type <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="boxedValue"/> is not of type <typeparamref name="T"/>.</exception>
    [return: NotNullIfNotNull(nameof(boxedValue))]
    public static BoxedValue<T>? GetTypedReference(object? boxedValue) => boxedValue switch
    {
        null => null,
        T => Unsafe.As<BoxedValue<T>>(boxedValue),
        _ => throw new ArgumentException(ExceptionMessages.BoxedValueTypeExpected<T>(), nameof(boxedValue)),
    };

    /// <summary>
    /// Converts a value type to an object reference.
    /// </summary>
    /// <param name="value">The value to be boxed.</param>
    /// <returns>A boxed representation of the value.</returns>
    public static BoxedValue<T> Box(T value) => Unsafe.As<BoxedValue<T>>(value);

    /// <summary>
    /// Boxes nullable value type to an object.
    /// </summary>
    /// <param name="value">The value to be boxed.</param>
    /// <returns>A boxed representation of the value.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static BoxedValue<T>? TryBox(in T? value) => Unsafe.As<BoxedValue<T>?>(value);

    /// <summary>
    /// Unboxes the value.
    /// </summary>
    /// <param name="boxedValue">The boxed representation of the value.</param>
    public static implicit operator T(BoxedValue<T> boxedValue) => boxedValue.Value;

    /// <summary>
    /// Converts a value type to an object reference.
    /// </summary>
    /// <param name="value">The value to be boxed.</param>
    /// <returns>A boxed representation of the value.</returns>
    public static explicit operator BoxedValue<T>(T value) => Box(value);

    /// <summary>
    /// Converts a typed reference to a boxed value to untyped reference.
    /// </summary>
    /// <param name="boxedValue">The boxed value.</param>
    /// <returns>Untyped reference to a boxed value.</returns>
    [return: NotNullIfNotNull(nameof(boxedValue))]
    public static implicit operator ValueType?(BoxedValue<T>? boxedValue)
        => Unsafe.As<ValueType>(boxedValue);

    /// <summary>
    /// Boxes nullable value type to an object.
    /// </summary>
    /// <param name="value">The value to be boxed.</param>
    /// <returns>A boxed representation of the value.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static explicit operator BoxedValue<T>?(in T? value) => TryBox(in value);

    /// <summary>
    /// Obtains a reference to the boxed value.
    /// </summary>
    /// <param name="boxedValue">Boxed value.</param>
    /// <returns>Mutable reference to the boxed value.</returns>
    public static implicit operator ValueReference<T>(BoxedValue<T> boxedValue)
        => new(boxedValue, ref boxedValue.Value);
    
    /// <inheritdoc />
    public abstract override bool Equals([NotNullWhen(true)] object? obj);  // abstract to avoid inlining by AOT/JIT

    /// <inheritdoc />
    public abstract override int GetHashCode(); // abstract to avoid inlining by AOT/JIT

    /// <inheritdoc />
    public abstract override string ToString(); // abstract to avoid inlining by AOT/JIT
}

/// <summary>
/// Represents extension methods for <see cref="BoxedValue{T}"/> class.
/// </summary>
public static class BoxedValue
{
    /// <summary>
    /// Unboxes the value.
    /// </summary>
    /// <param name="boxedValue">A reference to the boxed value.</param>
    /// <typeparam name="T">The value type.</typeparam>
    /// <returns>A reference to the boxed value.</returns>
    public static ref T Unbox<T>(this BoxedValue<T> boxedValue)
        where T : struct
        => ref boxedValue.Value;

    /// <summary>
    /// Creates a bitwise copy of the boxed value.
    /// </summary>
    /// <returns>A reference to bitwise copy of the boxed value.</returns>
    public static BoxedValue<T> Copy<T>(this BoxedValue<T> boxedValue) where T : struct
        => Unsafe.As<BoxedValue<T>>(MemberwiseClone(boxedValue));

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = nameof(MemberwiseClone))]
    private static extern object MemberwiseClone(object target);
}