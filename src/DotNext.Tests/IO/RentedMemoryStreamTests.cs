using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class RentedMemoryStreamTests : Test
    {
        [Fact]
        public static void BufferOverflow()
        {
            using var ms = new RentedMemoryStream(10);
            True(ms.Capacity >= 10);
            var buffer = new byte[ms.Capacity + 1];
            Throws<NotSupportedException>(() => ms.Write(buffer));
            ms.Write(new byte[9]);
            Equal(9, ms.Length);
        }
    }
}
