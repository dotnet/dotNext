using System.Diagnostics.CodeAnalysis;

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
        public static void EmptyResult2()
        {
            var r = default(Result<int, EnvironmentVariableTarget>);
            Equal(default(EnvironmentVariableTarget), r.Error);
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
        public static void RaiseError2()
        {
            var r = new Result<decimal, EnvironmentVariableTarget>(EnvironmentVariableTarget.Machine);
            Equal(EnvironmentVariableTarget.Machine, Throws<UndefinedResultException<EnvironmentVariableTarget>>(() => r.Value).ErrorCode);
            Equal(EnvironmentVariableTarget.Machine, r.Error);
            Equal(20M, r.Or(20M));
            Equal(0M, r.OrDefault());
            Null(r.OrNull());
        }

        [Fact]
        public static void Operators()
        {
            var result = new Result<int>(10);
            if (result) { }
            else Fail("Unexpected Result state");
            Equal(10, (int)result);
            Equal("10", result.ToString());
            Optional<int> opt = result;
            Equal(10, opt);
            Equal(10, result.OrInvoke(static () => 20));
            result = new Result<int>(new Exception());
            Equal(20, result.OrInvoke(static () => 20));
            opt = result;
            False(opt.HasValue);
        }

        [Fact]
        public static void Operators2()
        {
            var result = new Result<int, EnvironmentVariableTarget>(10);
            if (result) { }
            else Fail("Unexpected Result state");
            Equal(10, (int)result);
            Equal("10", result.ToString());
            Optional<int> opt = result;
            Equal(10, opt);
            Equal(10, result.OrInvoke(static () => 20));
            result = new Result<int, EnvironmentVariableTarget>(EnvironmentVariableTarget.Machine);
            Equal(20, result.OrInvoke(static () => 20));
            opt = result;
            False(opt.HasValue);
        }

        [Fact]
        public static void Boxing()
        {
            Equal("Hello", new Result<string>("Hello").Box());
            Null(new Result<string>(default(string)).Box().Value);
            IsType<ArithmeticException>(new Result<int>(new ArithmeticException()).Box().Error);

            Equal("Hello", new Result<string, EnvironmentVariableTarget>("Hello").Box());
            Null(new Result<string>(default(string)).Box().Value);
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
            Equal("Hello, world!", Optional.Create<string, Result<string>>(result));
        }

        [Fact]
        public static void OptionalInterop2()
        {
            Result<string, EnvironmentVariableTarget> result = "Hello, world!";
            Optional<string> opt = result;
            Equal("Hello, world!", opt);

            opt = Optional.Create<string, Result<string, EnvironmentVariableTarget>>(result);
            Equal("Hello, world!", opt);

            result = new(EnvironmentVariableTarget.Machine);
            opt = result;
            False(opt.HasValue);
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

            type = Result.GetUnderlyingType(typeof(Result<float, EnvironmentVariableTarget>));
            Equal(typeof(float), type);
        }

        [Fact]
        public static unsafe void Conversion()
        {
            Result<float> result = 20F;
            Equal(20, result.Convert(Convert.ToInt32));

            result = new(new Exception());
            False(result.Convert(&Convert.ToInt32).IsSuccessful);
        }

        [Fact]
        public static unsafe void Conversion2()
        {
            Result<float, EnvironmentVariableTarget> result = 20F;
            Equal(20, result.Convert(Convert.ToInt32));

            result = new(EnvironmentVariableTarget.Machine);
            Equal(EnvironmentVariableTarget.Machine, result.Convert(&Convert.ToInt32).Error);
        }

        [Fact]
        public static void HandleException()
        {
            Result<int> result = 20;
            Equal(20, result.OrInvoke(static e => 10));

            result = new(new ArithmeticException());
            Equal(10, result.OrInvoke(static e => 10));
        }

        [Fact]
        public static void HandleException2()
        {
            Result<int, EnvironmentVariableTarget> result = 20;
            Equal(20, result.OrInvoke(static e => 10));

            result = new(EnvironmentVariableTarget.Machine);
            Equal(10, result.OrInvoke(static e => 10));
        }
    }
}
