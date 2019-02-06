using System;

namespace DotNext
{
    public static class Predicate
    {
        private static class Cached<T>
        {
            internal static readonly Predicate<T> True = input => true;
            internal static readonly Predicate<T> False = input => false;
        }

        public static Predicate<T> True<T>() => Cached<T>.True;

        public static Predicate<T> False<T>() => Cached<T>.False;

        public static Func<T, bool> AsFunc<T>(this Predicate<T> predicate)
            => predicate.ChangeType<Func<T, bool>>();

        public static Converter<T, bool> AsConverter<T>(this Predicate<T> predicate)
            => predicate.ChangeType<Converter<T, bool>>();
    }
}
