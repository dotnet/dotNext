using System;

namespace DotNext
{
    public static class Converter
    {
        private static class Id<I, O>
            where I : O
        {
            internal static readonly Converter<I, O> Value = Delegates.Identity<I, O>;
        }

        public static Converter<I, O> Identity<I, O>()
            where I : O
            => Id<I, O>.Value;

        public static Converter<T, T> Identity<T>() => Identity<T, T>();

        public static Func<I, O> AsFunc<I, O>(this Converter<I, O> converter)
            => converter.ChangeType<Func<I, O>>();

        public static Predicate<T> AsPredicate<T>(this Converter<T, bool> converter)
            => converter.ChangeType<Predicate<T>>();
    }
}
