using System;

namespace DotNext
{
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
            internal static readonly Predicate<T> Value = Objects.IsNull;
        }

        private static class IsNotNullPredicate<T>
            where T: class
        {
            internal static readonly Predicate<T> Value = Objects.IsNotNull;
        }

        public static Predicate<T> IsNull<T>() 
            where T: class
            => IsNullPredicate<T>.Value;

        public static Predicate<T> IsNotNull<T>()
            where T: class
            => IsNotNullPredicate<T>.Value;

        public static Predicate<T> True<T>() => TruePredicate<T>.Value;

        public static Predicate<T> False<T>() => FalsePredicate<T>.Value;

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
