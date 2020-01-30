using System;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Xunit;

namespace DotNext.Text
{
    [ExcludeFromCodeCoverage]
    public sealed class EncodingDecodingContextTests : Test
    {
        [Fact]
        public static void EncodingContextInstantiation()
        {
            var context = new EncodingContext(Encoding.UTF8);
            Equal(Encoding.UTF8, context.Encoding);
            object clone = ((ICloneable)context).Clone();
            IsType<EncodingContext>(clone);
            Same(context.Encoding, ((EncodingContext)clone).Encoding);
        }

        [Fact]
        public static void DecodingContextInstantiation()
        {
            var context = new DecodingContext(Encoding.UTF8);
            Equal(Encoding.UTF8, context.Encoding);
            object clone = ((ICloneable)context).Clone();
            IsType<DecodingContext>(clone);
            Same(context.Encoding, ((DecodingContext)clone).Encoding);
        }
    }
}