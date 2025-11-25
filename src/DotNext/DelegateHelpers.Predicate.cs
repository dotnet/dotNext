using System.Diagnostics.CodeAnalysis;

namespace DotNext;

partial class DelegateHelpers
{
    /// <summary>
    /// Represents extensions for <see cref="Predicate{T}"/> type.
    /// </summary>
    /// <param name="predicate">The predicate to extend.</param>
    /// <typeparam name="T">The input type of the predicate.</typeparam>
    extension<T>(Predicate<T> predicate) where T : allows ref struct
    {
        /// <summary>
        /// Converts function to async delegate.
        /// </summary>
        /// <returns>The asynchronous function that wraps <paramref name="predicate"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <see langword="null"/>.</exception>
        public Func<T, CancellationToken, ValueTask<bool>> ToAsync()
        {
            ArgumentNullException.ThrowIfNull(predicate);

            return predicate.Invoke;
        }

        private ValueTask<bool> Invoke(T arg, CancellationToken token)
        {
            ValueTask<bool> task;
            if (token.IsCancellationRequested)
            {
                task = ValueTask.FromCanceled<bool>(token);
            }
            else
            {
                try
                {
                    task = new(predicate.Invoke(arg));
                }
                catch (Exception e)
                {
                    task = ValueTask.FromException<bool>(e);
                }
            }

            return task;
        }

        /// <summary>
        /// Invokes predicate without throwing the exception.
        /// </summary>
        /// <param name="obj">The object to compare against the criteria defined within the method represented by this delegate.</param>
        /// <returns><see langword="true"/> if <paramref name="obj" /> meets the criteria defined within the method represented by this delegate; otherwise, <see langword="false" />.</returns>
        public Result<bool> TryInvoke(T obj)
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

        /// <summary>
        /// Represents predicate as type <see cref="Func{T,Boolean}"/>.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A delegate of type <see cref="Func{T,Boolean}"/> referencing the same method as original predicate.</returns>
        public Func<T, bool> AsFunc()
            => predicate.ChangeType<Func<T, bool>>();

        /// <summary>
        /// Represents predicate as type <see cref="Converter{T,Boolean}"/>.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A delegate of type <see cref="Converter{T,Boolean}"/> referencing the same method as original predicate.</returns>
        public Converter<T, bool> AsConverter()
            => predicate.ChangeType<Converter<T, bool>>();
        
        /// <summary>
        /// Returns a predicate which negates evaluation result of
        /// the original predicate.
        /// </summary>
        /// <typeparam name="T">Type of the predicate argument.</typeparam>
        /// <param name="other">The predicate to negate.</param>
        /// <returns>The predicate which negates evaluation result of the original predicate.</returns>
        public static Predicate<T> operator !(Predicate<T> other)
        {
            ArgumentNullException.ThrowIfNull(other);

            return other.Negate;
        }

        private bool Negate(T obj) => !predicate(obj);

        /// <summary>
        /// Returns a predicate which computes logical AND between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical AND operand.</param>
        /// <param name="y">The second predicate acting as logical AND operand.</param>
        /// <returns>The predicate which computes logical AND between results of two other predicates.</returns>
        public static Predicate<T> operator &(Predicate<T> x, Predicate<T> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).And;
        }

        /// <summary>
        /// Returns a predicate which computes logical OR between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical OR operand.</param>
        /// <param name="y">The second predicate acting as logical OR operand.</param>
        /// <returns>The predicate which computes logical OR between results of two other predicates.</returns>
        public static Predicate<T> operator |(Predicate<T> x, Predicate<T> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).Or;
        }

        /// <summary>
        /// Returns a predicate which computes logical XOR between
        /// results of two other predicates.
        /// </summary>
        /// <param name="x">The first predicate acting as logical XOR operand.</param>
        /// <param name="y">The second predicate acting as logical XOR operand.</param>
        /// <returns>The predicate which computes logical XOR between results of two other predicates.</returns>
        public static Predicate<T> operator ^(Predicate<T> x, Predicate<T> y)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(y);

            return new BinaryOperator<T>(x, y).Xor;
        }

        /// <summary>
        /// Returns a predicate which always returns the specified value.
        /// </summary>
        /// <typeparam name="T">The type of the input parameter.</typeparam>
        /// <param name="value">The value to be returned by the predicate.</param>
        /// <returns>A cached predicate always returning <paramref name="value"/>.</returns>
        public static Predicate<T> Constant(bool value)
            => value ? True : False;
    }

    /// <summary>
    /// Providers static methods for <see cref="Predicate{T}"/> type. 
    /// </summary>
    extension<T>(Predicate<T>) where T : class
    {
        /// <summary>
        /// Checks whether the specified object is <see langword="null"/>.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns><see langword="true"/> if <paramref name="obj"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsNull([NotNullWhen(false)] T? obj) => obj is null;

        /// <summary>
        /// Checks whether the specified object is not <see langword="null"/>.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns><see langword="false"/> if <paramref name="obj"/> is <see langword="null"/>; otherwise, <see langword="true"/>.</returns>
        public static bool IsNotNull([NotNullWhen(true)] T? obj) => obj is not null;
    }

    /// <summary>
    /// Providers static methods for <see cref="Predicate{T}"/> type.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    extension<T>(Predicate<T>) where T : struct
    {
        /// <summary>
        /// Checks whether the specified object is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The object to check.</param>
        /// <returns><see langword="true"/> if <paramref name="value"/> is <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
        public static bool IsNotNull([NotNullWhen(true)] T? value) => value.HasValue;
    }
}

file sealed class BinaryOperator<T>(Predicate<T> left, Predicate<T> right)
    where T : allows ref struct
{
    internal bool Or(T value) => left(value) || right(value);

    internal bool And(T value) => left(value) && right(value);

    internal bool Xor(T value) => left(value) ^ right(value);
}