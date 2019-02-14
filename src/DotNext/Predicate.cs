using System;

namespace DotNext
{
    /// <summary>
    /// Provides extension methods for type <see cref="Predicate{T}"/> and
    /// predefined predicates.
    /// </summary>
    public static class Predicate
    {
        private static class TruePredicate<T>
        {
            internal static readonly Predicate<T> Value = input => true;
        }

        private static class FalsePredicate<T>
        {
            internal static readonly Predicate<T> Value = input => false;
        }

        private static class IsNullPredicate<T>
            where T: class
        {
            internal static readonly Predicate<T> Value = ObjectExtensions.IsNull;
        }

        private static class IsNotNullPredicate<T>
            where T: class
        {
            internal static readonly Predicate<T> Value = ObjectExtensions.IsNotNull;
        }

        /// <summary>
        /// Returns predicate implementing nullability check.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A predicate.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Predicate<T> IsNull<T>() 
            where T: class
            => IsNullPredicate<T>.Value;

        public static Predicate<T> IsNotNull<T>()
            where T: class
            => IsNotNullPredicate<T>.Value;

        /// <summary>
        /// Returns a predicate which always returns <see langword="true"/>.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A predicate which always returns <see langword="true"/>.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Predicate<T> True<T>() => TruePredicate<T>.Value;

        /// <summary>
        /// Returns a predicate which always returns <see langword="false"/>.
        /// </summary>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A predicate which always returns <see langword="false"/>.</returns>
        /// <remarks>
        /// This method returns the same instance of predicate on every call.
        /// </remarks>
        public static Predicate<T> False<T>() => FalsePredicate<T>.Value;
        
        /// <summary>
        /// Represents predicate as type <see cref="Func{T,Boolean}"/>.
        /// </summary>
        /// <param name="predicate">A predicate to convert.</param>
        /// <typeparam name="T">Type of predicate argument.</typeparam>
        /// <returns>A delegate of type <see cref="Func{T,Boolean}"/> referencing the same method as original predicate.</returns>
        public static Func<T, bool> AsFunc<T>(this Predicate<T> predicate)
            => predicate.ChangeType<Func<T, bool>>();

        public static Converter<T, bool> AsConverter<T>(this Predicate<T> predicate)
            => predicate.ChangeType<Converter<T, bool>>();

        public static Predicate<T> Negate<T>(this Predicate<T> predicate) => input => !predicate(input);

        public static Predicate<T> Or<T>(this Predicate<T> left, Predicate<T> right) => input => left(input) || right(input);

        public static Predicate<T> And<T>(this Predicate<T> left, Predicate<T> right) => input => left(input) && right(input);

        public static Predicate<T> Xor<T>(this Predicate<T> left, Predicate<T> right) => input => left(input) ^ right(input);
    }
}
