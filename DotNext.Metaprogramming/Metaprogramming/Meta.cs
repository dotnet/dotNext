using System;

namespace DotNext.Metaprogramming
{
    public abstract class Meta
    {
        public static Addition operator +(Meta left, Meta right)
            => null;
    }
}
