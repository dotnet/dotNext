using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;

namespace DotNext;

/// <summary>
/// Various extension methods for core data types.
/// </summary>
public static class BasicExtensions
{
    internal static bool IsNull(object? obj) => obj is null;

    internal static bool IsNotNull(object? obj) => obj is not null;

    internal static bool IsTypeOf<T>(object? obj) => obj is T;

    internal static TOutput Identity<TInput, TOutput>(TInput input)
        where TInput : TOutput
        => input;

    /// <summary>
    /// Provides ad-hoc approach to associate some data with the object
    /// without modification of it.
    /// </summary>
    /// <remarks>
    /// This method allows to associate arbitrary user data with any object.
    /// User data storage is not a part of object type declaration.
    /// Modification of user data doesn't cause modification of internal state of the object.
    /// The storage is associated with the object reference.
    /// Any user data are transient and can't be passed across process boundaries (i.e. serialization is not supported).
    /// </remarks>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">Target object.</param>
    /// <returns>User data storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UserDataStorage GetUserData<T>(this T obj)
        where T : class
        => new(obj);

    /// <summary>
    /// Checks whether the specified object is equal to one
    /// of the specified objects.
    /// </summary>
    /// <remarks>
    /// This method uses <see cref="object.Equals(object, object)"/>
    /// to check equality between two objects.
    /// </remarks>
    /// <typeparam name="T">The type of object to compare.</typeparam>
    /// <param name="value">The object to compare with other.</param>
    /// <param name="candidates">Candidate objects.</param>
    /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="candidates"/>.</returns>
    public static bool IsOneOf<T>(this T value, ReadOnlySpan<T> candidates)
    {
        foreach (var other in candidates)
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
                return true;
        }

        return false;
    }

    internal static bool IsContravariant(object? obj, Type type) => obj?.GetType().IsAssignableFrom(type) ?? false;

    /// <summary>
    /// Reinterprets object reference.
    /// </summary>
    /// <remarks>
    /// This method can be used to get access to the explicitly implemented
    /// interface methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// MemoryManager&lt;T&gt; manager;
    /// manager.As&lt;IDisposable&gt;().Dispose();
    /// </code>
    /// </example>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="obj">The object reference to reinterpret.</param>
    /// <returns>The reinterpreted <paramref name="obj"/> reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [return: NotNullIfNotNull(nameof(obj))]
    public static T As<T>(this T obj)
        where T : class?
        => obj;

    internal static TOutput ChangeType<TInput, TOutput>(this TInput input)
        where TInput : struct, IConvertible
        where TOutput : struct, IConvertible
        => (TOutput)input.ToType(typeof(TOutput), InvariantCulture);

    /// <summary>
    /// Attempts to get value from nullable container.
    /// </summary>
    /// <typeparam name="T">The underlying value type of the nullable type.</typeparam>
    /// <param name="nullable">Nullable value.</param>
    /// <param name="value">Underlying value.</param>
    /// <returns><see langword="true"/> if <paramref name="nullable"/> is not <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetValue<T>([NotNullWhen(true)] this T? nullable, out T value)
        where T : struct
    {
        value = Nullable.GetValueRefOrDefaultRef(in nullable);
        return nullable.HasValue;
    }

    /// <summary>
    /// Indicates that array is <see langword="null"/> or empty.
    /// </summary>
    /// <param name="array">The array to check.</param>
    /// <returns><see langword="true"/>, if array is <see langword="null"/> or empty.</returns>
    public static bool IsNullOrEmpty([NotNullWhen(false)] this Array? array)
        => array is null || Runtime.Intrinsics.GetLength(array) is 0;

    /// <summary>
    /// Determines whether the specified value is in the specified range.
    /// </summary>
    /// <example>
    /// The following example demonstrates how to check whether the value is in range [0..1).
    /// <code>
    /// double x;
    /// IsBetween(x, 0D.Enclosed(), 1D.Disclosed());
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <typeparam name="TLowerBound">The lower bound type.</typeparam>
    /// <typeparam name="TUpperBound">The upper bound type.</typeparam>
    /// <param name="value">The value to compare.</param>
    /// <param name="lowerBound">The lower bound.</param>
    /// <param name="upperBound">The upper bound.</param>
    /// <returns><see langword="true"/> if <paramref name="value"/> is in the specified range; otherwise, <see langword="false"/>.</returns>
    /// <seealso cref="Enclosed{T}(T)"/>
    /// <seealso cref="Disclosed{T}(T)"/>
    public static bool IsBetween<T, TLowerBound, TUpperBound>(this T value, TLowerBound lowerBound, TUpperBound upperBound)
        where T : notnull
        where TLowerBound : IRangeEndpoint<T>
        where TUpperBound : IRangeEndpoint<T>
        => lowerBound.IsOnRight(value) && upperBound.IsOnLeft(value);

    /// <summary>
    /// Creates enclosed range endpoint.
    /// </summary>
    /// <typeparam name="T">The type of the endpoint.</typeparam>
    /// <param name="value">The endpoint value.</param>
    /// <returns>The range endpoint.</returns>
    /// <seealso cref="IsBetween{T, TLowerBound, TUpperBound}(T, TLowerBound, TUpperBound)"/>
    public static EnclosedEndpoint<T> Enclosed<T>(this T value)
        where T : IComparable<T>
        => new() { Value = value };

    /// <summary>
    /// Creates disclosed range endpoint.
    /// </summary>
    /// <typeparam name="T">The type of the endpoint.</typeparam>
    /// <param name="value">The endpoint value.</param>
    /// <returns>The range endpoint.</returns>
    /// <seealso cref="IsBetween{T, TLowerBound, TUpperBound}(T, TLowerBound, TUpperBound)"/>
    public static DisclosedEndpoint<T> Disclosed<T>(this T value)
        where T : IComparable<T>
        => new() { Value = value };
}