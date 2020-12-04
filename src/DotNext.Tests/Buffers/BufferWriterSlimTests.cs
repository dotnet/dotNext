using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class BufferWriterSlimTests : Test
    {
        [Fact]
        public static void GrowableBuffer()
        {
            using var builder = new BufferWriterSlim<int>(stackalloc int[2], false);
            Equal(0, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(2, builder.FreeCapacity);

            builder.Write(new int[] { 10, 20 });
            Equal(2, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(0, builder.FreeCapacity);

            Equal(10, builder[0]);
            Equal(20, builder[1]);

            builder.Write(new int[] { 30, 40 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(30, builder[2]);
            Equal(40, builder[3]);
            Span<int> result = stackalloc int[5];
            Equal(4, builder.CopyTo(result));
            Equal(new int[] { 10, 20, 30, 40, 0 }, result.ToArray());

            var exceptionThrown = false;
            try
            {
                builder.WrittenSpan.ToArray();
            }
            catch (NotSupportedException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);

            builder.Clear(true);
            Equal(0, builder.WrittenCount);
            builder.Write(new int[] { 50, 60, 70, 80 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(50, builder[0]);
            Equal(60, builder[1]);
            Equal(70, builder[2]);
            Equal(80, builder[3]);

            builder.Clear(false);
            Equal(0, builder.WrittenCount);
            builder.Write(new int[] { 10, 20, 30, 40 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(10, builder[0]);
            Equal(20, builder[1]);
            Equal(30, builder[2]);
            Equal(40, builder[3]);
        }

        [Fact]
        public static void GrowableCopyingBuffer()
        {
            using var builder = new BufferWriterSlim<int>(stackalloc int[2], true);
            Equal(0, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(2, builder.FreeCapacity);

            builder.Write(new int[] { 10, 20 });
            Equal(2, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(0, builder.FreeCapacity);

            Equal(10, builder[0]);
            Equal(20, builder[1]);

            builder.Write(new int[] { 30, 40 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(30, builder[2]);
            Equal(40, builder[3]);
            Span<int> result = stackalloc int[5];
            Equal(4, builder.CopyTo(result));
            Equal(new int[] { 10, 20, 30, 40, 0 }, result.ToArray());

            builder.Clear(true);
            Equal(0, builder.WrittenCount);
            builder.Write(new int[] { 50, 60, 70, 80 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(50, builder[0]);
            Equal(60, builder[1]);
            Equal(70, builder[2]);
            Equal(80, builder[3]);

            builder.Clear(false);
            Equal(0, builder.WrittenCount);
            builder.Write(new int[] { 10, 20, 30, 40 });
            Equal(4, builder.WrittenCount);
            True(builder.Capacity >= 2);
            Equal(10, builder[0]);
            Equal(20, builder[1]);
            Equal(30, builder[2]);
            Equal(40, builder[3]);
        }

        [Fact]
        public static void EmptyBuilder()
        {
            using var builder = new BufferWriterSlim<int>();
            Equal(0, builder.Capacity);
            builder.Add(10);
            Equal(1, builder.WrittenCount);
            Equal(10, builder[0]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void DrainToStream(bool copyOnOverflow)
        {
            var expected = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            using var builder = new BufferWriterSlim<byte>(stackalloc byte[8], copyOnOverflow);
            builder.Write(expected);

            using var ms = new MemoryStream(8);
            builder.CopyTo(ms);
            Equal(8, ms.Length);
            Equal(expected, ms.ToArray());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void DrainToBuffer(bool copyOnOverflow)
        {
            var expected = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            using var builder = new BufferWriterSlim<byte>(stackalloc byte[8], copyOnOverflow);
            builder.Write(expected);

            var writer = new ArrayBufferWriter<byte>();
            builder.CopyTo(writer);
            Equal(8, writer.WrittenCount);
            Equal(expected, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static void DrainToStringBuilder()
        {
            const string expected = "Hello, world!";
            using var builder = new BufferWriterSlim<char>(stackalloc char[2], true);
            builder.Write(expected);

            var sb = new StringBuilder();
            builder.CopyTo(sb);

            Equal(expected, sb.ToString());
        }

        [Fact]
        public static void DrainToWriter()
        {
            const string expected = "Hello, world!";
            using var builder = new BufferWriterSlim<char>(stackalloc char[2], false, MemoryPool<char>.Shared.ToAllocator());
            builder.Write(expected);

            var sb = new StringWriter();
            builder.CopyTo(sb);

            Equal(expected, sb.ToString());
        }

        [Fact]
        public static void DrainToSpanWriter()
        {
            var expected = new byte[] { 1, 2, 3, 4 };
            using var builder = new BufferWriterSlim<byte>(stackalloc byte[4], false);
            builder.Write(expected);

            var writer = new SpanWriter<byte>(stackalloc byte[4]);
            Equal(4, builder.CopyTo(ref writer));
            Equal(expected, writer.Span.ToArray());
            Equal(expected, writer.WrittenSpan.ToArray());
        }
    }
}