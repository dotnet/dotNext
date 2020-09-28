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
    public sealed class ChunkSequenceTests : Test
    {
        [Fact]
        [Obsolete("This is the test for deprecated data type")]
        public static void EmptySequence()
        {
            var sequence = default(ChunkSequence<char>);
            Empty(sequence);
        }

        [Fact]
        [Obsolete("This is the test for deprecated method")]
        public static void AsReadOnlySequence()
        {
            var sequence = StringExtensions.Split("abcde", 2).ToReadOnlySequence();
            Equal(5, sequence.Length);
        }

        [Fact]
        [Obsolete("This is the test for deprecated method")]
        public static void SequenceEnumeration()
        {
            var index = 0;
            foreach (var segment in StringExtensions.Split("abcde", 2))
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
        [Obsolete("This is the test for deprecated method")]
        public static void SplitMemory()
        {
            var sequence = (ReadOnlySequence<char>)StringExtensions.Split("abcde", 2);
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
        [Obsolete("This is the test for deprecated method")]
        public static async Task CopyByteChunksToStream()
        {
            var bytes = new ChunkSequence<byte>(Encoding.UTF8.GetBytes("Hello, world!"), 3);
            using var content = new MemoryStream();
            await ChunkSequence.CopyToAsync(bytes, content).ConfigureAwait(false);
            content.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(content, Encoding.UTF8, false, 1024, true);
            Equal("Hello, world!", reader.ReadToEnd());
        }

        [Fact]
        [Obsolete("This is the test for deprecated method")]
        public static async Task CopyCharChunksToStream()
        {
            var bytes = new ChunkSequence<char>("Hello, world!".AsMemory(), 3);
            var sb = new StringBuilder();
            using (var writer = new StringWriter(sb))
                await ChunkSequence.CopyToAsync(bytes, writer).ConfigureAwait(false);
            Equal("Hello, world!", sb.ToString());
        }

        [Fact]
        public static void Concatenation()
        {
            ReadOnlyMemory<byte> block1 = default, block2 = default;
            Equal(Array.Empty<byte>(), block1.Concat(block2).ToArray());

            block1 = new byte[] {1, 2};
            Equal(new byte[] {1, 2}, block1.Concat(block2).ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] {1, 2}, block1.Concat(block2).ToArray());

            block1 = new byte[] {3, 4};
            Equal(new byte[] {1, 2, 3, 4}, block2.Concat(block1).ToArray());
        }

        [Fact]
        public static void Concatenation2()
        {
            ReadOnlyMemory<byte> block1 = default, block2 = default;
            Equal(Array.Empty<byte>(), new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block1 = new byte[] {1, 2};
            Equal(new byte[] {1, 2}, new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block2 = block1;
            block1 = default;
            Equal(new byte[] {1, 2}, new[] {block1, block2}.ToReadOnlySequence().ToArray());

            block1 = new byte[] {3, 4};
            Equal(new byte[] {1, 2, 3, 4}, new[] {block2, block1}.ToReadOnlySequence().ToArray());
        }
    }
}
