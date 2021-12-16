using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class OptionalTest : Test
    {
        [Fact]
        public static void NullableTest()
        {
            False(new Optional<int?>(null).HasValue);
            True(new Optional<long?>(10L).HasValue);
        }

        [Fact]
        public static void UndefinedOrNull()
        {
            Optional<int?> value = default;
            False(value.HasValue);
            True(value.IsUndefined);
            False(value.IsNull);
            False(value.TryGet(out var result, out var isNull));
            False(isNull);

            value = Optional<int?>.None;
            False(value.HasValue);
            True(value.IsUndefined);
            False(value.IsNull);
            False(value.TryGet(out result, out isNull));
            False(isNull);

            value = new Optional<int?>(null);
            False(value.HasValue);
            False(value.IsUndefined);
            True(value.IsNull);
            False(value.TryGet(out result, out isNull));
            True(isNull);
        }

        [Fact]
        public static void OptionalTypeTest()
        {
            var intOptional = new int?(10).ToOptional();
            True(intOptional.HasValue);
            False(intOptional.IsUndefined);
            False(intOptional.IsNull);
            Equal(10, (int)intOptional);
            Equal(10, intOptional.Or(20));
            Equal(10, intOptional.Value);
            Equal(10, intOptional.OrThrow(() => new ArithmeticException()));
            True(Nullable.Equals(10, intOptional.OrNull()));
            Equal(typeof(int), Optional.GetUnderlyingType(intOptional.GetType()));

            intOptional = default(int?).ToOptional();
            False(intOptional.HasValue);
            Equal(20, intOptional.Or(20));
            True(Nullable.Equals(null, intOptional.OrNull()));
            Equal(30, intOptional.Coalesce(new int?(30).ToOptional()).Value);
            Equal(40, (intOptional | new int?(40).ToOptional()).Value);
            Throws<InvalidOperationException>(() => intOptional.Value);

            Optional<string> strOptional = null;
            False(strOptional.HasValue);
            Equal("Hello, world", strOptional.Or("Hello, world"));
            Throws<InvalidOperationException>(() => strOptional.Value);
            Throws<ArithmeticException>(() => strOptional.OrThrow(() => new ArithmeticException()));
            Equal(typeof(string), Optional.GetUnderlyingType(strOptional.GetType()));
        }

        [Fact]
        public static void StructTest()
        {
            True(new Optional<long>(default).HasValue);
            True(new Optional<Base64FormattingOptions>(Base64FormattingOptions.InsertLineBreaks).HasValue);
            True(new Optional<long>(42L).TryGet(out var result, out var isNull));
            Equal(42L, result);
            False(isNull);
        }

        [Fact]
        public static void ClassTest()
        {
            True(new Optional<Optional<string>>("").HasValue);
            False(new Optional<Optional<string>>(null).HasValue);
            False(new Optional<string>(default).HasValue);
            True(new Optional<string>(default).IsNull);
            True(new Optional<string>("").HasValue);
            False(new Optional<Delegate>(default).HasValue);
            True(new Optional<EventHandler>((sender, args) => { }).HasValue);
        }

        [Fact]
        public static void OrElse()
        {
            var result = new Optional<int>(10) || Optional<int>.None;
            True(result.HasValue);
            Equal(10, result.Value);

            result = Optional<int>.None || new Optional<int>(20);
            True(result.HasValue);
            Equal(20, result.Value);
        }

        [Fact]
        public static void EqualityComparison()
        {
            Optional<string> opt1 = "1";
            Optional<string> opt2 = "1";
            Equal(opt1, opt2);
            True(opt1 == opt2);
            opt1 = default;
            NotEqual(opt1, opt2);
            True(opt1 != opt2);
            opt2 = default;
            Equal(opt1, opt2);
            True(opt1 == opt2);
            False(opt1 != opt2);
        }

        [Fact]
        public static void OrDefault()
        {
            var opt = new Optional<int>(10);
            Equal(10, opt.OrDefault());
            True(opt.Equals(10));
            True(opt.Equals((object)10));
            True(opt.Equals(10, EqualityComparer<int>.Default));
            opt = default;
            Equal(0, opt.OrDefault());
            False(opt.Equals(0));
            False(opt.Equals((object)0));
            False(opt.Equals(0, EqualityComparer<int>.Default));

            Equal(10, opt.OrInvoke(() => 10));
            opt = 20;
            Equal(20, opt.OrInvoke(() => 10));
        }

        [Fact]
        public static async Task TaskInterop()
        {
            var opt = new Optional<int>(10);
            Equal(10, await Task.FromResult(opt).OrDefault());
            Equal(10, await Task.FromResult(opt).OrNull());
            opt = default;
            Equal(0, await Task.FromResult(opt).OrDefault());
            Equal(10, await Task.FromResult(opt).OrInvoke(() => 10));
            Null(await Task.FromResult(opt).OrNull());
            opt = 20;
            Equal(20, await Task.FromResult(opt).OrInvoke(() => 10));
            Equal(20, await Task.FromResult(opt).OrThrow<int, ArithmeticException>());
            Equal(20, await Task.FromResult(opt).OrThrow(() => new ArithmeticException()));
            opt = default;
            await ThrowsAsync<ArithmeticException>(Task.FromResult(opt).OrThrow<int, ArithmeticException>);
            await ThrowsAsync<ArithmeticException>(() => Task.FromResult(opt).OrThrow(() => new ArithmeticException()));
        }

        [Fact]
        public static void Boxing()
        {
            False(Optional<string>.None.Box().HasValue);
            False(Optional<int>.None.Box().HasValue);
            False(Optional<int?>.None.Box().HasValue);
            Equal("123", new Optional<string>("123").Box());
            Equal(42, new Optional<int>(42).Box());
            Equal(42, new Optional<int?>(42).Box());
        }

        [Fact]
        public static void EqualityOperators()
        {
            Optional<string> first = default, second = default;
            True(first == second);
            True(first.Equals(second));
            False(first != second);

            first = new Optional<string>(null);
            True(first != second);
            False(first == second);

            first = "Hello, world!";
            False(first == second);
            False(first.Equals(second));
            True(first != second);
        }

        [Fact]
        public static void MutualExclusion()
        {
            Optional<string> first = default, second = default, result;
            result = first ^ second;
            True(result.IsUndefined);

            first = new Optional<string>(null);
            result = first ^ second;
            False(result.IsUndefined);
            True(result.IsNull);

            second = new Optional<string>(null);
            result = first ^ second;
            True(result.IsUndefined);

            second = default;
            first = "Hello, world!";
            result = first ^ second;
            Equal("Hello, world!", result.Value);

            second = "abc";
            result = first ^ second;
            True(result.IsUndefined);
        }

        [Fact]
        public static void NoneSomeNull()
        {
            Equal(Optional<int>.None, Optional.None<int>());
            Equal(new Optional<int>(20), Optional.Some<int>(20));
            Equal(new Optional<string>(null), Optional.Null<string>());
        }

        [Fact]
        public static void GettingReference()
        {
            var optional = Optional<int>.None;
            Throws<InvalidOperationException>(() => Optional.GetReference<int, InvalidOperationException>(optional));
            Throws<InvalidOperationException>(() => Optional.GetReference(in optional));
            optional = 23;
            Equal(23, Optional.GetReference<int, InvalidOperationException>(optional));
            Equal(23, Optional.GetReference(optional, static () => new InvalidOperationException()));
            Equal(23, Optional.GetReference(in optional));
        }

        [Fact]
        public static void HashCodes()
        {
            Equal(Optional.None<int>().GetHashCode(), Optional.None<string>().GetHashCode());
            Equal(Optional.Null<string>().GetHashCode(), Optional.Null<object>().GetHashCode());
            NotEqual(Optional.Null<string>().GetHashCode(), Optional.None<string>().GetHashCode());
            Equal(Optional.Some("Hello, world!"), Optional.Some("Hello, world!"));
        }

        [Fact]
        public static void ValueCheck()
        {
            True(Optional<string>.IsValueDefined("Hello, world"));
            False(Optional<string>.IsValueDefined(null));

            True(Optional<int>.IsValueDefined(default));

            True(Optional<int?>.IsValueDefined(42));
            False(Optional<int?>.IsValueDefined(null));

            True(Optional<Optional<int>>.IsValueDefined(42));
            False(Optional<Optional<int>>.IsValueDefined(Optional.None<int>()));
        }
    }
}
