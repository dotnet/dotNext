using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.VariantType
{
    [ExcludeFromCodeCoverage]
    public sealed class VariantTests : Test
    {
        [Fact]
        public static void DynamicNull()
        {
            dynamic variant = default(Variant<string, Uri>);
            True(variant == null);
        }

        [Fact]
        public static void DynamicVariant()
        {
            Variant<string, Uri> variant = "Hello, world!";
            dynamic d = variant;
            Equal(5, d.IndexOf(','));
            variant = new Uri("http://contoso.com");
            d = variant;
            Equal("http", d.Scheme);
            Variant<string, Uri, Version> variant2 = variant;
            Equal(new Uri("http://contoso.com"), (Uri)variant2);
            Null((string)variant2);
            Null((Version)variant2);
        }

        [Fact]
        public static void Permutation()
        {
            var obj = new Variant<string, object>("Hello");
            var obj2 = obj.Permute();
            Equal<object>(obj, obj2);
            Equal("Hello", obj.First);
            Equal("Hello", obj.Second);
            Equal(obj.GetHashCode(), obj2.GetHashCode());
            Equal(obj.ToString(), obj2.ToString());
        }

        [Fact]
        public static void NullCheck()
        {
            Variant<string, object> obj = new object();
            False(obj.IsNull);
            obj = default;
            True(obj.IsNull);
        }

        [Fact]
        public static void Conversion()
        {
            Variant<string, object> obj = new object();
            var result = obj.Convert<string>(Func.Identity<string>().AsConverter(), static value => value.ToString());
            True(result.HasValue);
            NotNull(result.Value);
            obj = default;
            result = obj.Convert<string>(Func.Identity<string>().AsConverter(), static value => value.ToString());
            False(result.HasValue);
        }

        [Fact]
        public static void Deconstruction()
        {
            var (x, y) = new Variant<string, object>(new object());
            Null(x);
            NotNull(y);

            var (x2, y2, z2) = new Variant<string, object, Uri>("Hello");
            NotNull(x2);
            IsType<string>(y2);
            Null(z2);

            var (x3, y3, z3, k3) = new Variant<string, object, Uri, OperatingSystem>(Environment.OSVersion);
            Null(x3);
            NotNull(y3);
            Null(z3);
            NotNull(k3);
        }
    }
}