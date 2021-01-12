using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ResultTests : Assert
    {
        [Fact]
        public static void EmptyResult()
        {
            var r = default(Result<int>);
            Null(r.Error);
            True(r.IsSuccessful);
            Equal(0, r.Value);
            Equal(default, r);
            True(r.TryGet(out _));
        }

        [Fact]
        public static void Choice()
        {
            var r1 = new Result<int>(10);
            var r2 = new Result<int>(new Exception());
            False(r2.IsSuccessful);
            True(r1.IsSuccessful);
            True(r1.TryGet(out var x));
            Equal(10, x);
            False(r2.TryGet(out x));
            Equal(0, x);
            var r = r1.Coalesce(r2);
            True(r.IsSuccessful);
            True(r.TryGet(out x));
            Equal(10, x);
            NotNull(r.OrNull());
        }

        [Fact]
        public static void RaiseError()
        {
            var r = new Result<decimal>(new ArithmeticException());
            Throws<ArithmeticException>(() => r.Value);
            NotNull(r.Error);
            Equal(20M, r.Or(20M));
            Equal(0M, r.OrDefault());
            Null(r.OrNull());
        }
    }
}
