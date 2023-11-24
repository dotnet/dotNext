using Debug = System.Diagnostics.Debug;

namespace DotNext;

/// <summary>
/// Provides extension methods for type <see cref="Predicate{T}"/> and
/// predefined predicates.
/// </summary>
public static class Predicate
{
    /// <summary>
    /// Gets a predicate that can be used to check whether the specified object is of specific type.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The predicate instance.</returns>
    public static Predicate<object?> IsTypeOf<T>() => BasicExtensions.IsTypeOf<T>;

    /// <summary>
    /// Returns predicate implementing nullability check.
    /// </summary>
    /// <typeparam name="T">Type of predicate argument.</typeparam>
    /// <returns>The predicate instance.</returns>
    /// <remarks>
    /// This method returns the same instance of predicate on every call.
    /// </remarks>
    public static Predicate<T> IsNull<T>()
        where T : class?
        => BasicExtensions.IsNull;

    /// <summary>
    /// Returns predicate checking that input argument
    /// is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the predicate argument.</typeparam>
    /// <returns>The predicate instance.</returns>
    /// <remarks>
    /// This method returns the same instance of predicate on every call.
    /// </remarks>
    public static Predicate<T> IsNotNull<T>()
        where T : class?
        => BasicExtensions.IsNotNull;

    /// <summary>
    /// Returns predicate checking that input argument of value type
    /// is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The type of the predicate argument.</typeparam>
    /// <returns>The predicate instance.</returns>
    /// <remarks>
    /// This method returns the same instance of predicate on every call.
    /// </remarks>
    public static Predicate<T?> HasValue<T>()
        where T : struct
    {
        return HasValueCore;

        static bool HasValueCore(T? value) => value.HasValue;
    }

    /// <summary>
    /// Returns a predicate which always returns the specified value.
    /// </summary>
    /// <typeparam name="T">The type of the input parameter.</typeparam>
    /// <param name="value">The value to be returned by the predicate.</param>
    /// <returns>A cached predicate always returning <paramref name="value"/>.</returns>
    public static Predicate<T> Constant<T>(bool value)
    {
        return value ? True : False;

        static bool True(T value) => true;

        static bool False(T value) => false;
    }

    /// <summary>
    /// Represents predicate as type <see cref="Func{T,Boolean}"/>.
    /// </summary>
    /// <param name="predicate">A predicate to convert.</param>
    /// <typeparam name="T">Type of predicate argument.</typeparam>
    /// <returns>A delegate of type <see cref="Func{T,Boolean}"/> referencing the same method as original predicate.</returns>
    public static Func<T, bool> AsFunc<T>(this Predicate<T> predicate)
        => predicate.ChangeType<Func<T, bool>>();

    /// <summary>
    /// Represents predicate as type <see cref="Converter{T,Boolean}"/>.
    /// </summary>
    /// <param name="predicate">A predicate to convert.</param>
    /// <typeparam name="T">Type of predicate argument.</typeparam>
    /// <returns>A delegate of type <see cref="Converter{T,Boolean}"/> referencing the same method as original predicate.</returns>
    public static Converter<T, bool> AsConverter<T>(this Predicate<T> predicate)
        => predicate.ChangeType<Converter<T, bool>>();

    private static bool Negate<T>(this Predicate<T> predicate, T obj)
        => !predicate(obj);

    /// <summary>
    /// Returns a predicate which negates evaluation result of
    /// the original predicate.
    /// </summary>
    /// <typeparam name="T">Type of the predicate argument.</typeparam>
    /// <param name="predicate">The predicate to negate.</param>
    /// <returns>The predicate which negates evaluation result of the original predicate.</returns>
    public static Predicate<T> Negate<T>(this Predicate<T> predicate)
        => predicate is not null ? predicate.Negate : throw new ArgumentNullException(nameof(predicate));

    private sealed class BinaryOperator<T>
    {
        private readonly Predicate<T> left, right;

        internal BinaryOperator(Predicate<T> left, Predicate<T> right)
        {
            Debug.Assert(left is not null);
            Debug.Assert(right is not null);

            this.left = left;
            this.right = right;
        }

        internal bool Or(T value) => left(value) || right(value);

        internal bool And(T value) => left(value) && right(value);

        internal bool Xor(T value) => left(value) ^ right(value);
    }

    /// <summary>
    /// Returns a predicate which computes logical OR between
    /// results of two other predicates.
    /// </summary>
    /// <typeparam name="T">Type of the predicate argument.</typeparam>
    /// <param name="left">The first predicate acting as logical OR operand.</param>
    /// <param name="right">The second predicate acting as logical OR operand.</param>
    /// <returns>The predicate which computes logical OR between results of two other predicates.</returns>
    public static Predicate<T> Or<T>(this Predicate<T> left, Predicate<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinaryOperator<T>(left, right).Or;
    }

    /// <summary>
    /// Returns a predicate which computes logical AND between
    /// results of two other predicates.
    /// </summary>
    /// <typeparam name="T">Type of the predicate argument.</typeparam>
    /// <param name="left">The first predicate acting as logical AND operand.</param>
    /// <param name="right">The second predicate acting as logical AND operand.</param>
    /// <returns>The predicate which computes logical AND between results of two other predicates.</returns>
    public static Predicate<T> And<T>(this Predicate<T> left, Predicate<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinaryOperator<T>(left, right).And;
    }

    /// <summary>
    /// Returns a predicate which computes logical XOR between
    /// results of two other predicates.
    /// </summary>
    /// <typeparam name="T">Type of the predicate argument.</typeparam>
    /// <param name="left">The first predicate acting as logical XOR operand.</param>
    /// <param name="right">The second predicate acting as logical XOR operand.</param>
    /// <returns>The predicate which computes logical XOR between results of two other predicates.</returns>
    public static Predicate<T> Xor<T>(this Predicate<T> left, Predicate<T> right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        return new BinaryOperator<T>(left, right).Xor;
    }

    /// <summary>
    /// Invokes predicate without throwing the exception.
    /// </summary>
    /// <typeparam name="T">The type of the object to compare.</typeparam>
    /// <param name="predicate">The predicate to invoke.</param>
    /// <param name="obj">The object to compare against the criteria defined within the method represented by this delegate.</param>
    /// <returns><see langword="true"/> if <paramref name="obj" /> meets the criteria defined within the method represented by this delegate; otherwise, <see langword="false" />.</returns>
    public static Result<bool> TryInvoke<T>(this Predicate<T> predicate, T obj)
    {
        Result<bool> result;
        try
        {
            result = predicate(obj);
        }
        catch (Exception e)
        {
            result = new(e);
        }

        return result;
    }
}