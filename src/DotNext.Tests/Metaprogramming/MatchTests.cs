using System.Linq.Expressions;

namespace DotNext.Metaprogramming;

using Linq.Expressions;
using static CodeGenerator;

public sealed class MatchTests : Test
{
    [Fact]
    public static void TypeBasedPattern()
    {
        var lambda = Lambda<Func<object, int>>(static fun =>
        {
            Match(fun[0])
                .Case<string>(static t => t.CollectionLength)
                .Default((-1).Quoted)
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
                .Case<string>(t => result.Assign(t.CollectionLength))
                .Default(result.Assign((-1).Quoted))
            .End();
        }).Compile();
        Equal(5, lambda("abcde"));
        Equal(-1, lambda(3));
    }

    private struct Point
    {
        internal long X, Y;
    }

    [Fact]
    public static void StructMatch()
    {
        var lambda = Lambda<Func<Point, string>>(static fun =>
        {
            Match(fun[0])
                .Case("X", 0L.Quoted, static _ => "X is zero".Quoted)
                .Case(new { X = long.MaxValue, Y = long.MaxValue }, new MatchBuilder.CaseStatement(static _ => "MaxValue".Quoted))
                .Default("Unknown".Quoted)
                .OfType<string>()
            .End();
        }).Compile();
        Equal("X is zero", lambda(new Point { X = 0, Y = 20 }));
        Equal("X is zero", lambda(new Point { X = 0, Y = 30 }));
        Equal("MaxValue", lambda(new Point { X = long.MaxValue, Y = long.MaxValue }));
        Equal("Unknown", lambda(new Point { X = 10 }));
    }

    [Fact]
    public static void StructMatchVoid()
    {
        var lambda = Lambda<Func<Point, string>>((fun, result) =>
        {
            Match(fun[0])
                .Case("X", 0L.Quoted, x =>
                {
                    Assign(result, "X is zero".Quoted);
                })
                .Case("X", long.MaxValue.Quoted, "Y", long.MaxValue.Quoted, (x, y) =>
                {
                    Assign(result, "MaxValue".Quoted);
                })
                .Default(new Action<ParameterExpression>(_ => Assign(result, "Unknown".Quoted)))
            .End();
        }).Compile();
        Equal("X is zero", lambda(new Point { X = 0, Y = 10 }));
        Equal("MaxValue", lambda(new Point { X = long.MaxValue, Y = long.MaxValue }));
        Equal("Unknown", lambda(new Point { X = 10 }));
    }

    [Fact]
    public static void TupleMatch()
    {
        var lambda = Lambda<Func<(long X, long Y), string>>(static fun =>
        {
            Match(fun[0])
                .Case("Item1", 0L.Quoted, static value => "X is zero".Quoted)
                .Case((long.MaxValue, long.MaxValue), new MatchBuilder.CaseStatement(static value => "MaxValue".Quoted))
                .Default("Unknown".Quoted)
                .OfType<string>()
            .End();
        }).Compile();
        Equal("X is zero", lambda((0, 20)));
        Equal("X is zero", lambda((0, 30)));
        Equal("MaxValue", lambda((long.MaxValue, long.MaxValue)));
        Equal("Unknown", lambda((10, 0)));
    }
}