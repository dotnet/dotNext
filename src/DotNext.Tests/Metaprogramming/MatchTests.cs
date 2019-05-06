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
                    .Case<string>(t => t.Count())
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
                    .Case<string>(t => result.Assign(t.Count()))
                    .Default(result.Assign((-1).Const()))
                .End();
            }).Compile();
            Equal(5, lambda("abcde"));
            Equal(-1, lambda(3));
        }

        private struct Point
        {
            public long X, Y;
        }

        [Fact]
        public static void TupleMatch()
        {
            var lambda = Lambda<Func<Point, string>>(fun =>
            {
                Match(fun[0])
                    .Case(new { X = 0L }, value => "X is zero".Const())
                    .Case(new { X = long.MaxValue, Y = long.MaxValue }, value => "MaxValue".Const())
                    .Default("Unknown".Const())
                    .OfType<string>()
                .End();
            }).Compile();
            Equal("X is zero", lambda(new Point { X = 0, Y = 20 }));
            Equal("X is zero", lambda(new Point { X = 0, Y = 30 }));
            Equal("MaxValue", lambda(new Point { X = long.MaxValue, Y = long.MaxValue }));
            Equal("Unknown", lambda(new Point { X = 10 }));
        }
    }
}