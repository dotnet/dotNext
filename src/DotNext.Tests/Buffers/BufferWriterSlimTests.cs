using System.Diagnostics.CodeAnalysis;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class BufferWriterSlimTests : Test
    {
        [Fact]
        public static void GrowableBuffer()
        {
            using var builder = new BufferWriterSlim<int>(stackalloc int[2]);
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
            builder.WrittenSpan.CopyTo(result, out var writtenCount);
            Equal(4, writtenCount);
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

        [Fact]
        public static void ReadWritePrimitives()
        {
            var builder = new BufferWriterSlim<byte>(stackalloc byte[512]);
            try
            {
                builder.WriteInt16(short.MinValue, true);
                builder.WriteInt16(short.MaxValue, false);
                builder.WriteUInt16(42, true);
                builder.WriteUInt16(ushort.MaxValue, false);
                builder.WriteInt32(int.MaxValue, true);
                builder.WriteInt32(int.MinValue, false);
                builder.WriteUInt32(42, true);
                builder.WriteUInt32(uint.MaxValue, false);
                builder.WriteInt64(long.MaxValue, true);
                builder.WriteInt64(long.MinValue, false);
                builder.WriteUInt64(42, true);
                builder.WriteUInt64(ulong.MaxValue, false);
                builder.WriteSingle(float.MaxValue, true);
                builder.WriteSingle(float.MinValue, false);
                builder.WriteDouble(double.MaxValue, true);
                builder.WriteDouble(double.MinValue, false);

                var reader = new SpanReader<byte>(builder.WrittenSpan);
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
            finally
            {
                builder.Dispose();
            }
        }

        [Fact]
        public static void EscapeBuffer()
        {
            using var buffer = new BufferWriterSlim<int>(stackalloc int[2]);
            buffer.Add(10);
            buffer.Add(20);
            False(buffer.TryDetachBuffer(out var owner));

            buffer.Add(30);
            True(buffer.TryDetachBuffer(out owner));
            Equal(0, buffer.WrittenCount);
            Equal(10, owner[0]);
            Equal(20, owner[1]);
            Equal(30, owner[2]);
            Equal(3, owner.Length);
            owner.Dispose();
        }
    }
}