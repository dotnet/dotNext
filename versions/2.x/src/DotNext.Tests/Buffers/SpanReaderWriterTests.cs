using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class SpanReaderTests : Test
    {
        [Fact]
        public static unsafe void WriteAndGet()
        {
            var writer = new SpanWriter<int>(stackalloc int[5]);
            Equal(0, writer.WrittenCount);
            Equal(5, writer.FreeCapacity);
            ref int current = ref writer.Current;
            
            writer.Add(10);
            Equal(1, writer.WrittenCount);
            Equal(4, writer.FreeCapacity);
            Equal(10, current);
            
            var segment = writer.Slide(4);
            segment[0] = 20;
            segment[1] = 30;
            segment[2] = 40;
            segment[3] = 50;
            Equal(5, writer.WrittenCount);
            Equal(0, writer.FreeCapacity);
            Equal(new int[] { 10, 20, 30, 40, 50 }, writer.WrittenSpan.ToArray());

            var exceptionThrown = false;
            try
            {
                writer.Add(42);
            }
            catch (EndOfStreamException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);

            writer.Reset();
            Equal(0, writer.WrittenCount);
        }

        [Fact]
        public static void ReadWrite()
        {
            var writer = new SpanWriter<byte>(stackalloc byte[3]);
            var expected = new byte[] { 10, 20, 30 };
            Equal(3, writer.Write(expected));

            var reader = new SpanReader<byte>(writer.Span);
            Equal(3, reader.RemainingCount);
            Equal(0, reader.ConsumedCount);
            True(reader.ConsumedSpan.IsEmpty);
            Equal(10, reader.Current);

            Equal(10, reader.Read());
            Equal(20, reader.Current);
            Equal(2, reader.RemainingCount);
            Equal(1, reader.ConsumedCount);

            Equal(new byte[] { 10 }, reader.ConsumedSpan.ToArray());
            Equal(new byte[] { 20, 30 }, reader.Read(2).ToArray());

            Equal(0, reader.Read(new byte[2]));

            reader.Reset();
            Equal(0, reader.ConsumedCount);
            
            var actual = new byte[3];
            Equal(3, reader.Read(actual));
            Equal(expected, actual);
        }

        [Fact]
        public static unsafe void EncodingDecodingBlittableType()
        {
            var writer = new SpanWriter<byte>(stackalloc byte[sizeof(Guid)]);
            var expected = Guid.NewGuid();
            True(writer.TryWrite(in expected));

            var reader = new SpanReader<byte>(writer.Span);
            True(reader.TryRead(out Guid actual));
            Equal(expected, actual);

            writer.Reset();
            reader.Reset();
            writer.Write(in expected);
            Equal(expected, reader.Read<Guid>());
        }

        [Fact]
        public static void EmptyReader()
        {
            var reader = new SpanReader<byte>();
            Equal(0, reader.RemainingCount);
            Equal(0, reader.ConsumedCount);
            Equal(Array.Empty<byte>(), reader.ReadToEnd().ToArray());

            var exceptionThrown = false;
            try
            {
                reader.Current.ToString();
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);
            False(reader.TryRead(new byte[1]));
            False(reader.TryRead(1, out _));
            False(reader.TryRead(out _));
            False(reader.TryRead(out Guid value));

            Equal(0, reader.Read(new byte[1]));

            exceptionThrown = false;
            try
            {
                reader.Read(10);
            }
            catch (EndOfStreamException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);
        }

        [Fact]
        public static void EmptyWriter()
        {
            var writer = new SpanWriter<byte>();
            Equal(0, writer.WrittenCount);
            Equal(0, writer.FreeCapacity);

            var exceptionThrown = false;
            try
            {
                writer.Current.ToString();
            }
            catch (InvalidOperationException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);
            False(writer.TryWrite(new byte[1]));
            False(writer.TryWrite(1));
            False(writer.TrySlide(2, out _));
            False(writer.TryAdd(1));

            Equal(0, writer.Write(new byte[1]));

            exceptionThrown = false;

            try
            {
                writer.Slide(2);
            }
            catch (EndOfStreamException)
            {
                exceptionThrown = true;
            }

            True(exceptionThrown);
        }

        [Fact]
        public static void ReadToEnd()
        {
            var reader = new SpanReader<int>(new[] { 10, 20, 30 });
            Equal(new[] { 10, 20, 30 }, reader.ReadToEnd().ToArray());
            reader.Reset();
            Equal(10, reader.Read());
            Equal(new[] { 20, 30}, reader.ReadToEnd().ToArray());
        }
    }
}