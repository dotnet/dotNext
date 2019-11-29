using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class OptionalTest : Assert
    {
        [Fact]
        public static void NullableTest()
        {
            False(new Optional<int?>(null).IsPresent);
            True(new Optional<long?>(10L).IsPresent);
        }

        [Fact]
        public static void OptionalTypeTest()
        {
            var intOptional = new int?(10).ToOptional();
            True(intOptional.IsPresent);
            Equal(10, (int)intOptional);
            Equal(10, intOptional.Or(20));
            Equal(10, intOptional.Value);
            True(Nullable.Equals(10, intOptional.OrNull()));
            Equal(typeof(int), Optional.GetUnderlyingType(intOptional.GetType()));

            intOptional = default(int?).ToOptional();
            False(intOptional.IsPresent);
            Equal(20, intOptional.Or(20));
            True(Nullable.Equals(null, intOptional.OrNull()));
            Equal(30, intOptional.Coalesce(new int?(30).ToOptional()).Value);
            Equal(40, (intOptional | new int?(40).ToOptional()).Value);
            Throws<InvalidOperationException>(() => intOptional.Value);

            Optional<string> strOptional = null;
            False(strOptional.IsPresent);
            Equal("Hello, world", strOptional.Or("Hello, world"));
            Throws<InvalidOperationException>(() => strOptional.Value);
            Equal(typeof(string), Optional.GetUnderlyingType(strOptional.GetType()));
        }

        [Fact]
        public static void StructTest()
        {
            False(new Optional<ValueTuple>(default).IsPresent);
            True(new Optional<long>(default).IsPresent);
            True(new Optional<Base64FormattingOptions>(Base64FormattingOptions.InsertLineBreaks).IsPresent);
        }

        [Fact]
        public static void ClassTest()
        {
            True(new Optional<Optional<string>>("").IsPresent);
            False(new Optional<Optional<string>>(null).IsPresent);
            False(new Optional<string>(default).IsPresent);
            True(new Optional<string>("").IsPresent);
            False(new Optional<Delegate>(default).IsPresent);
            True(new Optional<EventHandler>((sender, args) => { }).IsPresent);
        }

        [Fact]
        public static void OrElse()
        {
            var result = new Optional<int>(10) || Optional<int>.Empty;
            True(result.IsPresent);
            Equal(10, result.Value);

            result = Optional<int>.Empty || new Optional<int>(20);
            True(result.IsPresent);
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
        public static void Serialization()
        {
            Optional<string> opt = default;
            False(SerializationTestHelper.SerializeDeserialize(opt).IsPresent);
            opt = "Hello";
            Equal("Hello", SerializationTestHelper.SerializeDeserialize(opt).Value);
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
            opt = default;
            await ThrowsAsync<ArithmeticException>(Task.FromResult(opt).OrThrow<int, ArithmeticException>);
        }
    }
}
