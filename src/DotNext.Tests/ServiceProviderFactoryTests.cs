using System.Globalization;

namespace DotNext;

public sealed class ServiceProviderFactoryTests : Test
{
    [Fact]
    public static void UseBuilderStyle()
    {
        var builder = IServiceProvider.CreateBuilder()
            .Add<IConvertible>("value")
            .Add<IFormattable>(22);

        var provider = builder.Build();
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        Null(provider.GetService(typeof(string)));

        builder.Reset();
        Same(IServiceProvider.Empty, builder.Build());
    }

    [Fact]
    public static void CreateInPlace1()
    {
        var provider = IServiceProvider.Create<IConvertible>("value");
        IsType<string>(provider.GetService(typeof(IConvertible)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void Factory1()
    {
        var factory = IServiceProvider.CreateFactory<IConvertible>();
        var provider = factory("value");
        IsType<string>(provider.GetService(typeof(IConvertible)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void DelegatingFactory1()
    {
        var factory = IServiceProvider.CreateDelegatingFactory<IConvertible>();
        var provider = factory("value", IServiceProvider.Empty);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace2()
    {
        var provider = IServiceProvider.Create<IConvertible, IFormattable>("value", 22);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void Factory2()
    {
        var factory = IServiceProvider.CreateFactory<IConvertible, IFormattable>();
        var provider = factory("value", 22);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void DelegatingFactory2()
    {
        var factory = IServiceProvider.CreateDelegatingFactory<IConvertible, IFormattable>();
        var provider = factory("value", 22, IServiceProvider.Empty);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace3()
    {
        var provider = IServiceProvider.Create<IConvertible, IFormattable, IComparable<long>>("value", 22, 42L);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void Factory3()
    {
        var factory = IServiceProvider.CreateFactory<IConvertible, IFormattable, IComparable<long>>();
        var provider = factory("value", 22, 42L);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void DelegatingFactory3()
    {
        var factory = IServiceProvider.CreateDelegatingFactory<IConvertible, IFormattable, IComparable<long>>();
        var provider = factory("value", 22, 42L, IServiceProvider.Empty);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace4()
    {
        var provider = IServiceProvider.Create<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>>("value", 22, 42L, decimal.Zero);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void Factory4()
    {
        var factory = IServiceProvider.CreateFactory<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>>();
        var provider = factory("value", 22, 42L, decimal.Zero);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void DelegatingFactory4()
    {
        var factory = IServiceProvider.CreateDelegatingFactory<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>>();
        var provider = factory("value", 22, 42L, decimal.Zero, IServiceProvider.Empty);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace5()
    {
        var provider = IServiceProvider.Create<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>, IFormatProvider>("value", 22, 42L, decimal.Zero, CultureInfo.InvariantCulture);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        IsAssignableFrom<CultureInfo>(provider.GetService(typeof(IFormatProvider)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void Factory5()
    {
        var factory = IServiceProvider.CreateFactory<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>, IFormatProvider>();
        var provider = factory("value", 22, 42L, decimal.Zero, CultureInfo.InvariantCulture);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        IsAssignableFrom<CultureInfo>(provider.GetService(typeof(IFormatProvider)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void DelegatingFactory5()
    {
        var factory = IServiceProvider.CreateDelegatingFactory<IConvertible, IFormattable, IComparable<long>, IEquatable<decimal>, IFormatProvider>();
        var provider = factory("value", 22, 42L, decimal.Zero, CultureInfo.InvariantCulture, IServiceProvider.Empty);
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        IsType<decimal>(provider.GetService(typeof(IEquatable<decimal>)));
        IsAssignableFrom<CultureInfo>(provider.GetService(typeof(IFormatProvider)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void FromTupleType()
    {
        var provider = IServiceProvider.FromTuple(new ValueTuple<IConvertible, IFormattable, IComparable<long>>("value", 22, 42L));
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
    }
}