using System;

namespace DotNext
{
    /// <summary>
    /// Provides various extensions for functional delegates.
    /// </summary>
    public static class Func
    {
        private static class Id<I, O>
            where I: O
        {
            internal static readonly Func<I, O> Value = Delegates.Identity<I, O>;
        }

        public static Func<I, O> Identity<I, O>()
            where I : O
            => Id<I, O>.Value;

        public static Func<T, T> Identity<T>() => Identity<T, T>();

        public static Predicate<T> AsPredicate<T>(this Func<T, bool> predicate)
            => predicate.ChangeType<Predicate<T>>();

        public static Converter<I, O> AsConverter<I, O>(this Func<I, O> function)
            => function.ChangeType<Converter<I, O>>();
    }
}
