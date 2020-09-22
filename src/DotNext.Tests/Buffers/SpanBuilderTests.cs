using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SpanBuilderTests : Test
    {
        [Fact]
        public static void FixedSizeBuilder()
        {
            using var builder = new SpanBuilder<int>(stackalloc int[4]);
            Equal(0, builder.WrittenCount);
            Equal(4, builder.Capacity);
            Equal(4, builder.FreeCapacity);
            False(builder.IsGrowable);

            builder.Write(new int[] { 10, 20 });
            Equal(2, builder.WrittenCount);
            Equal(4, builder.Capacity);
            Equal(2, builder.FreeCapacity);

            Equal(new int[] { 10, 20 }, builder.WrittenSpan.ToArray());

            builder.Add(30);
            builder.Add(40);
            Equal(4, builder.WrittenCount);
            Equal(4, builder.Capacity);
            Equal(0, builder.FreeCapacity);

            Equal(new int[] { 10, 20, 30, 40 }, builder.WrittenSpan.ToArray());
            
            var exceptionThrown = false;
            try
            {
                builder.Add(50);
            }
            catch (InsufficientMemoryException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);

            Span<int> result = stackalloc int[4];
            builder.DrainTo(result);
            Equal(new int[] { 10, 20, 30, 40 }, result.ToArray());
        }

        [Fact]
        public static void GrowableBuffer()
        {
            using var builder = new SpanBuilder<int>(stackalloc int[2], false);
            Equal(0, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(2, builder.FreeCapacity);
            True(builder.IsGrowable);

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
            builder.DrainTo(result);
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
        public static void GrowableCopyingBuffer()
        {
            using var builder = new SpanBuilder<int>(stackalloc int[2], true);
            Equal(0, builder.WrittenCount);
            Equal(2, builder.Capacity);
            Equal(2, builder.FreeCapacity);
            True(builder.IsGrowable);

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
            builder.DrainTo(result);
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
            using var builder = new SpanBuilder<int>();
            Equal(0, builder.Capacity);
            False(builder.IsGrowable);

            var exceptionThrown = false;
            try
            {
                builder.Add(10);
            }
            catch (InsufficientMemoryException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);

            exceptionThrown = false;
            try
            {
                Equal(10, builder[0]);
            }
            catch (ArgumentOutOfRangeException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);
        }

        [Fact]
        public static void DrainToStream()
        {
            var expected = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var builder = new SpanBuilder<byte>(stackalloc byte[8]);
            builder.Write(expected);

            using var ms = new MemoryStream(8);
            builder.DrainTo(ms);
            Equal(8, ms.Length);
            Equal(expected, ms.ToArray());
        }

        [Fact]
        public static void DrainToBuffer()
        {
            var expected = new byte[] { 10, 20, 30, 40, 50, 60, 70, 80 };
            var builder = new SpanBuilder<byte>(stackalloc byte[8]);
            builder.Write(expected);

            var writer = new ArrayBufferWriter<byte>();
            builder.DrainTo(writer);
            Equal(8, writer.WrittenCount);
            Equal(expected, writer.WrittenSpan.ToArray());
        }

        [Fact]
        public static void DrainToStringBuilder()
        {
            const string expected = "Hello, world!";
            var builder = new SpanBuilder<char>(stackalloc char[2], true);
            builder.Write(expected);

            var sb = new StringBuilder();
            builder.DrainTo(sb);

            Equal(expected, sb.ToString());
        }

        [Fact]
        public static void DrainToWriter()
        {
            const string expected = "Hello, world!";
            var builder = new SpanBuilder<char>(stackalloc char[2], false, MemoryPool<char>.Shared.ToAllocator());
            builder.Write(expected);

            var sb = new StringWriter();
            builder.DrainTo(sb);

            Equal(expected, sb.ToString());
        }
    }
}