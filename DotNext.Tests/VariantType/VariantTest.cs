using System;
using Xunit;

namespace DotNext.VariantType
{
    public sealed class VariantTest: Assert
    {   
        [Fact]
        public void DynamicTest()
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
    }
}