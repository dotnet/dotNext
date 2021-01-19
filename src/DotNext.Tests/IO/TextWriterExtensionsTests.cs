using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    using static Buffers.BufferHelpers;

    [ExcludeFromCodeCoverage]
    public sealed class TextWriterExtensionsTests : Test
    {
        [Fact]
        public static async Task WriteSequence()
        {
            var sequence = new [] { "abc".AsMemory(), "def".AsMemory(), "g".AsMemory() }.ToReadOnlySequence();
            await using var writer = new StringWriter();
            await writer.WriteAsync(sequence);
            Equal("abcdefg", writer.ToString());
        }
    }
}