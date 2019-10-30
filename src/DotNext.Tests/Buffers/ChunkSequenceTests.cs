using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class ChunkSequenceTests : Assert
    {
        [Fact]
        public static void EmptySequence()
        {
            var sequence = default(ChunkSequence<char>);
            Empty(sequence);
        }

        [Fact]
        public static void SequenceEnumeration()
        {
            var index = 0;
            foreach (var segment in "abcde".Split(2))
                switch (index++)
                {
                    case 0:
                        var array = segment.Span;
                        Equal(2, array.Length);
                        Equal('a', array[0]);
                        Equal('b', array[1]);
                        break;
                    case 1:
                        array = segment.Span;
                        Equal(2, array.Length);
                        Equal('c', array[0]);
                        Equal('d', array[1]);
                        break;
                    case 2:
                        array = segment.Span;
                        Equal(1, array.Length);
                        Equal('e', array[0]);
                        break;
                }
            Equal(3, index);
        }

        [Fact]
        public static void SplitMemory()
        {
            var sequence = (ReadOnlySequence<char>)"abcde".Split(2);
            False(sequence.IsSingleSegment);
            var index = 0;
            foreach (var segment in sequence)
                switch (index++)
                {
                    case 0:
                        var array = segment.Span;
                        Equal(2, array.Length);
                        Equal('a', array[0]);
                        Equal('b', array[1]);
                        break;
                    case 1:
                        array = segment.Span;
                        Equal(2, array.Length);
                        Equal('c', array[0]);
                        Equal('d', array[1]);
                        break;
                    case 2:
                        array = segment.Span;
                        Equal(1, array.Length);
                        Equal('e', array[0]);
                        break;
                }
            Equal(3, index);
        }

        [Fact]
        public static async Task CopyByteChunksToStream()
        {
            var bytes = new ChunkSequence<byte>(Encoding.UTF8.GetBytes("Hello, world!"), 3);
            using (var content = new MemoryStream())
            {
                await bytes.CopyToAsync(content).ConfigureAwait(false);
                content.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true))
                {
                    Equal("Hello, world!", reader.ReadToEnd());
                }
            }
        }

        [Fact]
        public static async Task CopyCharChunksToStream()
        {
            var bytes = new ChunkSequence<char>("Hello, world!".AsMemory(), 3);
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
                await bytes.CopyToAsync(writer).ConfigureAwait(false);
            Equal("Hello, world!", sb.ToString());
        }
    }
}
