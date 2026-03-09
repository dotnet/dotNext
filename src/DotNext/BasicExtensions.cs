using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;

namespace DotNext;

/// <summary>
/// Various extension methods for core data types.
/// </summary>
public static class BasicExtensions
{
    /// <summary>
    /// Extends reference types.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">Target object.</param>
    extension<T>(T obj)
        where T : class
    {
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
        /// <value>User data storage.</value>
        public UserDataStorage UserData => new(obj);
    }

    /// <summary>
    /// Checks whether the specified object is equal to one
    /// of the specified objects.
    /// </summary>
    /// <remarks>
    /// This method uses <see cref="object.Equals(object, object)"/>
    /// to check equality between two objects.
    /// </remarks>
    /// <typeparam name="T">The type of object to compare.</typeparam>
    /// <param name="value">The object to compare with the others.</param>
    /// <param name="candidates">Candidate objects.</param>
    /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="candidates"/>.</returns>
    public static bool IsOneOf<T>(this T value, params ReadOnlySpan<T> candidates)
    {
        foreach (var other in candidates)
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
                return true;
        }

        return false;
    }

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
    /// <seealso cref="get_Enclosed{T}(T)"/>
    /// <seealso cref="get_Disclosed{T}(T)"/>
    public static bool IsBetween<T, TLowerBound, TUpperBound>(this T value, TLowerBound lowerBound, TUpperBound upperBound)
        where T : notnull, allows ref struct
        where TLowerBound : IRangeEndpoint<T>, allows ref struct
        where TUpperBound : IRangeEndpoint<T>, allows ref struct
        => lowerBound.IsOnRight(value) && upperBound.IsOnLeft(value);

    /// <param name="value">The endpoint value.</param>
    /// <typeparam name="T">The type of the endpoint.</typeparam>
    extension<T>(T value) where T : IComparable<T>, allows ref struct
    {
        /// <summary>
        /// Creates enclosed range endpoint.
        /// </summary>
        /// <returns>The range endpoint.</returns>
        /// <seealso cref="IsBetween{T, TLowerBound, TUpperBound}(T, TLowerBound, TUpperBound)"/>
        public EnclosedEndpoint<T> Enclosed => new() { Value = value };

        /// <summary>
        /// Creates disclosed range endpoint.
        /// </summary>
        /// <returns>The range endpoint.</returns>
        /// <seealso cref="IsBetween{T, TLowerBound, TUpperBound}(T, TLowerBound, TUpperBound)"/>
        public DisclosedEndpoint<T> Disclosed => new() { Value = value };

        /// <summary>
        /// Gets the endpoint that represents the infinity.
        /// </summary>
        public static IRangeEndpoint<T> Unbounded => IRangeEndpoint<T>.Infinity;
    }
    
    /// <summary>
    /// Providers static methods for <see cref="Predicate{T}"/> type. 
    /// </summary>
    extension<T>(T)
        where T : notnull
    {
        /// <summary>
        /// Checks whether the specified object is of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns></returns>
        public static bool IsTypeOf([NotNullWhen(true)] object? obj) => obj is T;

        /// <summary>
        /// Checks whether the specified object is exactly of the specified type.
        /// </summary>
        /// <param name="obj">The object to test.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is not <see langword="null"/> and of type <typeparamref name="T"/>; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsExactTypeOf(object? obj) => obj?.GetType() == typeof(T);
    }

    /// <summary>
    /// Extends <see cref="GC"/>.
    /// </summary>
    extension(GC)
    {
        /// <summary>
        /// Keeps the reference to the value type alive.
        /// </summary>
        /// <param name="location">A location of the object.</param>
        /// <typeparam name="T">The value type.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void KeepAlive<T>(ref readonly T location)
            where T : struct, allows ref struct
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                KeepAliveImpl(in location);

            [MethodImpl(MethodImplOptions.NoInlining)]
            [StackTraceHidden]
            static void KeepAliveImpl(ref readonly T location)
            {
                // We cannot inline this check to avoid compiler optimization to eliminate null check.
                // This check can be eliminated because typically the location points to the field in the class
                // and that field is already statically checked for null
                if (Unsafe.IsNullRef(in location))
                    throw new ArgumentNullException(nameof(location));
            }
        }
    }
}