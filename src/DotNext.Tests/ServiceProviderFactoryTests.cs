namespace DotNext;

public sealed class ServiceProviderFactoryTests : Test
{
    [Fact]
    public static void UseBuilderStyle()
    {
        var builder = IServiceProvider.CreateBuilder()
            .Add(Func<IConvertible>.Constant("value"))
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
        var provider = IServiceProvider.Create<ValueTuple<IConvertible>>(new("value"));
        IsType<string>(provider.GetService(typeof(IConvertible)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace2()
    {
        var provider = IServiceProvider.Create<(IConvertible, IFormattable)>(("value", 22));
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void CreateInPlace3()
    {
        var provider = IServiceProvider.Create<(IConvertible, IFormattable, IComparable<long>)>(("value", 22, 42L));
        IsType<string>(provider.GetService(typeof(IConvertible)));
        IsType<int>(provider.GetService(typeof(IFormattable)));
        IsType<long>(provider.GetService(typeof(IComparable<long>)));
        Null(provider.GetService(typeof(string)));
    }

    [Fact]
    public static void OverrideEmptyWithInstance()
    {
        const string expected = "value";
        var provider = IServiceProvider.Empty.Override<IConvertible>(expected);
        Same(expected, provider.GetService(typeof(IConvertible)));
    }
    
    [Fact]
    public static void OverrideEmptyWithDelegate()
    {
        const string expected = "value";
        var provider = IServiceProvider.Empty.Override<IConvertible>(Func<string>.Constant(expected));
        Same(expected, provider.GetService(typeof(IConvertible)));
    }
    
    [Fact]
    public static void OverrideEmptyWithSupplier()
    {
        const string expected = "value";
        var provider = IServiceProvider.Empty.Override<IConvertible>(new ValueSupplier<IConvertible>(expected));
        Same(expected, provider.GetService(typeof(IConvertible)));
    }
}