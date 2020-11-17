using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class ServiceProviderBuilderTests : Test
    {
        [Fact]
        public static void UseBuilderStyle()
        {
            var builder = new ServiceProviderBuilder()
                .Add<IConvertible>("value")
                .Add<IFormattable>(22);

            var provider = builder.Build();
            IsType<string>(provider.GetService(typeof(IConvertible)));
            IsType<int>(provider.GetService(typeof(IFormattable)));
            Null(provider.GetService(typeof(string)));

            builder.Clear();
            Same(ServiceProviderBuilder.Empty, builder.Build());
        }

        [Fact]
        public static void UseFactoryStyle1()
        {
            var factory = ServiceProviderBuilder.CreateFactory(typeof(IConvertible), typeof(IFormattable));
            var provider = factory(new object[] { "value", 22 });
            IsType<string>(provider.GetService(typeof(IConvertible)));
            IsType<int>(provider.GetService(typeof(IFormattable)));
            Null(provider.GetService(typeof(string)));
        }

        [Fact]
        public static void UseFactoryStyle2()
        {
            var factory = ServiceProviderBuilder.CreateDelegatingFactory(typeof(IConvertible), typeof(IFormattable));
            var provider = factory(new object[] { "value", 22 }, ServiceProviderBuilder.Empty);
            IsType<string>(provider.GetService(typeof(IConvertible)));
            IsType<int>(provider.GetService(typeof(IFormattable)));
            Null(provider.GetService(typeof(string)));
        }
    }
}