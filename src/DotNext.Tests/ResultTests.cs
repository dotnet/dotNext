using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ResultTests : Test
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

        [Fact]
        public static void Serialization()
        {
            Result<decimal> r = 10M;
            Equal(10M, SerializeDeserialize(r).Value);
            r = new Result<decimal>(new ArithmeticException());
            IsType<ArithmeticException>(SerializeDeserialize(r).Error);
        }

        [Fact]
        public static void Operators()
        {
            var result = new Result<int>(10);
            if (result) { }
            else throw new Xunit.Sdk.XunitException();
            Equal(10, (int)result);
            Equal("10", result.ToString());
            Optional<int> opt = result;
            Equal(10, opt);
            Equal(10, result.OrInvoke(() => 20));
            result = new Result<int>(new Exception());
            Equal(20, result.OrInvoke(() => 20));
            opt = result;
            False(opt.HasValue);
        }

        [Fact]
        public static void Boxing()
        {
            Equal("Hello", new Result<string>("Hello").Box());
            Null(new Result<string>(default(string)).Box().Value);
            IsType<ArithmeticException>(new Result<int>(new ArithmeticException()).Box().Error);
        }

        [Fact]
        public static void OptionalInterop()
        {
            var result = (Result<string>)Optional<string>.None;
            False(result.IsSuccessful);
            Throws<InvalidOperationException>(() => result.Value);
            
            result = (Result<string>)new Optional<string>(null);
            True(result.IsSuccessful);
            Null(result.Value);

            result = (Result<string>)new Optional<string>("Hello, world!");
            True(result.IsSuccessful);
            Equal("Hello, world!", result.Value);
        }

        [Fact]
        public static void UnderlyingType()
        {
            var type = Result.GetUnderlyingType(typeof(Result<>));
            Null(type);
            type = Result.GetUnderlyingType(typeof(int));
            Null(type);
            type = Result.GetUnderlyingType(typeof(Result<string>));
            Equal(typeof(string), type);
        }
    }
}
