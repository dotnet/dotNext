﻿namespace DotNext;

using Runtime.CompilerServices;

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
        Equal(10, opt.ValueOrDefault);
        True(opt.Equals(10));
        True(opt.Equals((object)10));
        True(opt.Equals(10, EqualityComparer<int>.Default));
        opt = default;
        Equal(0, opt.ValueOrDefault);
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
        Equal(20D, await Task.FromResult(opt).Convert(double.CreateChecked));
        Equal(20D, await Task.FromResult(opt).Convert(FromInt));
        Equal(20, await Task.FromResult(opt).Or(42));
        Equal(20, await Task.FromResult(opt).If(int.IsEvenInteger));
        
        opt = default;
        Equal(42, await Task.FromResult(opt).Or(42));
        Equal(Optional<int>.None, await Task.FromResult(opt).If(int.IsEvenInteger));
        Equal(Optional<double>.None, await Task.FromResult(opt).Convert(FromInt));
        await ThrowsAsync<ArithmeticException>(Task.FromResult(opt).OrThrow<int, ArithmeticException>);
        await ThrowsAsync<ArithmeticException>(() => Task.FromResult(opt).OrThrow(static () => new ArithmeticException()));

        static Task<double> FromInt(int value, CancellationToken _) => Task.FromResult(double.CreateChecked(value));
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
        Throws<InvalidOperationException>(() => optional.GetReference<InvalidOperationException>());
        Throws<InvalidOperationException>(() => optional.ValueRef);
        optional = 23;
        Equal(23, optional.GetReference<InvalidOperationException>());
        Equal(23, optional.GetReference(static () => new InvalidOperationException()));
        Equal(23, optional.ValueRef);
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

    [Fact]
    public static void Flatten()
    {
        Optional<Optional<string>> input = default;
        Optional<string> output = input.Flatten();
        True(output.IsUndefined);

        input = new(default(string));
        output = input.Flatten();
        True(output.IsNull);

        input = new(string.Empty);
        output = input.Flatten();
        True(output.HasValue);
    }

    [Fact]
    public static void ConvertNoneToValueType()
    {
        var value = Optional<int>.None.Convert(static i => i.ToString());
        False(value.HasValue);
        False(value.IsNull);
    }

    [Fact]
    public static void ConvertValueTypeToValueType()
    {
        var value = new Optional<int>(42).Convert(static i => i + 1);
        True(value.HasValue);
        Equal(43, value.Value);
    }

    [Fact]
    public static unsafe void ConvertNullToValueType()
    {
        var value = new Optional<string>(null).Convert(&int.Parse);
        False(value.HasValue);
        False(value.IsNull);
    }

    [Fact]
    public static void ConvertNullToRefType()
    {
        var value = new Optional<string>(null).Convert(Converter.Identity<string, string>());
        False(value.HasValue);
        True(value.IsNull);
    }

    [Fact]
    public static void ConvertNoneToRefType()
    {
        var value = Optional<string>.None.Convert(Converter.Identity<string, string>());
        False(value.HasValue);
        False(value.IsNull);
    }

    [Fact]
    public static void ConvertToOptional()
    {
        Equal(Optional.None<double>(), Optional.None<int>().Convert(ToDouble));
        Equal(42D, new Optional<int>(42).Convert(ToDouble));

        static Optional<double> ToDouble(int value) => double.CreateChecked(value);
    }

    [Fact]
    public static void OptionalToDelegate()
    {
        IFunctional<Func<object>> functional = Optional.None<object>();
        Null(functional.ToDelegate().Invoke());

        functional = new Optional<int>(42);
        Equal(42, functional.ToDelegate().Invoke());

        functional = Optional.None<int>();
        Null(functional.ToDelegate().Invoke());
    }

    [Fact]
    public static void ConcatTwoValues()
    {
        Optional<(int, long)> optional = new Optional<int>(42).Concat<long>(43L);
        Equal((42, 43L), optional.Value);

        optional = Optional<int>.None.Concat<long>(42L);
        False(optional.HasValue);
    }

    [Fact]
    public static async Task FlattenTask()
    {
        await ThrowsAsync<InvalidOperationException>(static () => Task.FromResult(Optional.None<int>()).Flatten());
        Equal(42, await Task.FromResult(Optional.Some(42)).Flatten());
    }

    [Fact]
    public static void MiscOperators()
    {
        Optional<int> result = 20;
        False(!result);

        if (result)
        {
        }
        else
        {
            Fail("Optional has no value");
        }
        
        result = Optional<int>.None;
        True(!result);

        if (result)
        {
            Fail("Optional has value");
        }
    }
}