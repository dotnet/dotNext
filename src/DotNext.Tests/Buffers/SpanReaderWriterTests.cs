using System.Diagnostics.CodeAnalysis;
using System.Numerics;

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
            catch (InternalBufferOverflowException)
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
            catch (InternalBufferOverflowException)
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
            catch (InternalBufferOverflowException)
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
            Equal(new[] { 20, 30 }, reader.ReadToEnd().ToArray());
        }

        [Fact]
        public static void ReadWritePrimitives()
        {
            var buffer = new byte[1024];
            var writer = new SpanWriter<byte>(buffer);
            writer.WriteInt16(short.MinValue, true);
            writer.WriteInt16(short.MaxValue, false);
            writer.WriteUInt16(42, true);
            writer.WriteUInt16(ushort.MaxValue, false);
            writer.WriteInt32(int.MaxValue, true);
            writer.WriteInt32(int.MinValue, false);
            writer.WriteUInt32(42, true);
            writer.WriteUInt32(uint.MaxValue, false);
            writer.WriteInt64(long.MaxValue, true);
            writer.WriteInt64(long.MinValue, false);
            writer.WriteUInt64(42, true);
            writer.WriteUInt64(ulong.MaxValue, false);
            writer.WriteSingle(float.MaxValue, true);
            writer.WriteSingle(float.MinValue, false);
            writer.WriteDouble(double.MaxValue, true);
            writer.WriteDouble(double.MinValue, false);

            var reader = new SpanReader<byte>(buffer);
            Equal(short.MinValue, reader.ReadInt16(true));
            Equal(short.MaxValue, reader.ReadInt16(false));
            Equal(42, reader.ReadUInt16(true));
            Equal(ushort.MaxValue, reader.ReadUInt16(false));
            Equal(int.MaxValue, reader.ReadInt32(true));
            Equal(int.MinValue, reader.ReadInt32(false));
            Equal(42U, reader.ReadUInt32(true));
            Equal(uint.MaxValue, reader.ReadUInt32(false));
            Equal(long.MaxValue, reader.ReadInt64(true));
            Equal(long.MinValue, reader.ReadInt64(false));
            Equal(42UL, reader.ReadUInt64(true));
            Equal(ulong.MaxValue, reader.ReadUInt64(false));
            Equal(float.MaxValue, reader.ReadSingle(true));
            Equal(float.MinValue, reader.ReadSingle(false));
            Equal(double.MaxValue, reader.ReadDouble(true));
            Equal(double.MinValue, reader.ReadDouble(false));
        }

        [Fact]
        public static unsafe void TryWrite()
        {
            Span<byte> bytes = stackalloc byte[128];
            var writer = new SpanWriter<byte>(bytes);
            BigInteger value = 10L;
            True(writer.TryWrite(&WriteBigInt, value));
            Equal(value, new BigInteger(bytes.Slice(0, writer.WrittenCount)));

            static bool WriteBigInt(BigInteger value, Span<byte> destination, out int count)
                => value.TryWriteBytes(destination, out count);
        }

        [Fact]
        public static void AdvanceWriter()
        {
            var writer = new SpanWriter<byte>(stackalloc byte[4]);

            writer.Current = 10;
            writer.Advance(1);

            writer.Current = 20;
            writer.Advance(1);

            writer.Current = 30;
            writer.Advance(2);

            True(writer.RemainingSpan.IsEmpty);

            writer.Rewind(2);
            Equal(30, writer.Current);

            writer.Rewind(1);
            Equal(20, writer.Current);
        }

        [Fact]
        public static void AdvanceReader()
        {
            var reader = new SpanReader<byte>(new byte[] { 10, 20, 30 });
            Equal(10, reader.Current);

            reader.Advance(2);
            Equal(30, reader.Current);

            reader.Rewind(2);
            Equal(10, reader.Current);
        }
    }
}