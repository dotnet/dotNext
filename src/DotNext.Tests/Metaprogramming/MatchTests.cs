using System;
using Xunit;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;
    using static CodeGenerator;

    public sealed class MatchTests : Assert
    {
        [Fact]
        public static void TypeBasedPattern()
        {
            var lambda = Lambda<Func<object, int>>(fun =>
            {
                Match(fun[0])
                    .When<string>(t => t.Property(nameof(string.Length)))
                    .Default((-1).Const())
                    .OfType<int>()
                .End();
            }).Compile();
            Equal(3, lambda("abc"));
            Equal(-1, lambda(20));
        }

        [Fact]
        public static void TypeBasedVoidPattern()
        {
            var lambda = Lambda<Func<object, int>>((fun, result) =>
            {
                Match(fun[0])
                    .When<string>(t => result.Assign(t.Property(nameof(string.Length))))
                    .Default(result.Assign((-1).Const()))
                .End();
            }).Compile();
            Equal(5, lambda("abcde"));
            Equal(-1, lambda(3));
        }
    }
}