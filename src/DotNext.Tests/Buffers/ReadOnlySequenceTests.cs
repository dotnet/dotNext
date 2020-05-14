using System;
using System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class ReadOnlySequenceTests : Test
    {
        private static readonly byte[] data = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120 };

        public static IEnumerable<object[]> TestBuffers()
        {   
            yield return new object[] { new ReadOnlySequence<byte>(data) };
            yield return new object[] { new ChunkSequence<byte>(data, 2).ToReadOnlySequence() };
            yield return new object[] { new ChunkSequence<byte>(data, 3).ToReadOnlySequence() };
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static void CopyToStream(ReadOnlySequence<byte> sequence)
        {
            using var dest = new MemoryStream();
            using var src = sequence.AsStream();
            Equal(0L, src.Position);
            Equal(sequence.Length, src.Length);

            src.CopyTo(dest);
            dest.Position = 0;
            Equal(data, dest.ToArray());
        }

        [Fact]
        public static void EmptyCopyTo()
        {
            using var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>().AsStream();
            Equal(0L, src.Length);

            src.CopyTo(dest);
            Equal(0L, dest.Length);
        }

        [Fact]
        public static void SeekAndCopy()
        {
            using var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            NotEqual(0L, src.Length);
            src.Position = data.Length;

            src.CopyTo(dest);
            Equal(0L, dest.Length);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static async Task CopyToStreamAsync(ReadOnlySequence<byte> sequence)
        {
            using var dest = new MemoryStream();
            using var src = sequence.AsStream();

            await src.CopyToAsync(dest);
            dest.Position = 0;
            Equal(data, dest.ToArray());
        }
        
        [Fact]
        public static void CopyAfterReuse()
        {
            var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>(data).AsStream();

            src.CopyTo(dest);
            Equal(data, dest.ToArray());
            Equal(data.Length, src.Length);

            dest.Dispose();
            dest = new MemoryStream();
            src.Position = 0L;
            src.CopyTo(dest);
            Equal(data, dest.ToArray());
            Equal(data.Length, src.Length);
        }

        [Fact]
        public static void SeekFromEnd()
        {
            using var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            Equal(data.Length - 1, src.Seek(-1L, SeekOrigin.End));
            src.CopyTo(dest);
            Equal(1L, dest.Length);
            Equal(data[data.Length - 1], dest.ToArray()[0]);
        }

        [Fact]
        public static void SeekFromStart()
        {
            using var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            Equal(data.Length - 1, src.Seek(data.Length - 1, SeekOrigin.Begin));
            src.CopyTo(dest);
            Equal(1L, dest.Length);
            Equal(data[data.Length - 1], dest.ToArray()[0]);
        }

        [Fact]
        public static void SeekFromCurrent()
        {
            using var dest = new MemoryStream();
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            src.Position = 1;
            Equal(data.Length - 1, src.Seek(data.Length - 2, SeekOrigin.Current));
            src.CopyTo(dest);
            Equal(1L, dest.Length);
            Equal(data[data.Length - 1], dest.ToArray()[0]);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static void ReadSpan(ReadOnlySequence<byte> sequence)
        {
            using var src = sequence.AsStream();
            Span<byte> dest = new byte[data.Length];
            Equal(dest.Length, src.Read(dest));
            Equal(data, dest.ToArray());

            src.Position = sequence.Length - 1;
            Equal(1, src.Read(dest.Slice(0, 1)));
            Equal(data[data.Length - 1], dest[0]);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static void ReadArray(ReadOnlySequence<byte> sequence)
        {
            using var src = sequence.AsStream();
            var dest = new byte[data.Length];
            Equal(dest.Length, src.Read(dest, 0, dest.Length));
            Equal(data, dest);

            src.Position = sequence.Length - 1;
            Equal(1, src.Read(dest, 0, 1));
            Equal(data[data.Length - 1], dest[0]);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static async Task ReadMemory(ReadOnlySequence<byte> sequence)
        {
            using var src = sequence.AsStream();
            Memory<byte> dest = new byte[data.Length];
            Equal(dest.Length, await src.ReadAsync(dest));
            Equal(data, dest.ToArray());

            src.Position = sequence.Length - 1;
            Equal(1, await src.ReadAsync(dest.Slice(0, 1)));
            Equal(data[data.Length - 1], dest.Span[0]);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static async Task ReadArrayAsync(ReadOnlySequence<byte> sequence)
        {
            using var src = sequence.AsStream();
            var dest = new byte[data.Length];
            Equal(dest.Length, await src.ReadAsync(dest, 0, dest.Length));
            Equal(data, dest);

            src.Position = sequence.Length - 1;
            Equal(1, await src.ReadAsync(dest, 0, 1));
            Equal(data[data.Length - 1], dest[0]);
        }

        [Theory]
        [MemberData(nameof(TestBuffers))]
        public static void ReadSingleByte(ReadOnlySequence<byte> sequence)
        {
            using var src = sequence.AsStream();
            
            for (var i = 0; i < data.Length; i++)
                Equal(data[i], src.ReadByte());

            src.Seek(-1L, SeekOrigin.End);
            Equal(data[data.Length - 1], src.ReadByte());
            Equal(-1, src.ReadByte());
        }

        [Fact]
        public static void InvalidSeek()
        {
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            Throws<ArgumentOutOfRangeException>(() => src.Seek(500L, SeekOrigin.Begin));
            Throws<IOException>(() => src.Seek(-500L, SeekOrigin.End));
        }

        [Fact]
        public static async Task WriteNotSupported()
        {
            using var src = new ReadOnlySequence<byte>(data).AsStream();
            True(src.CanRead);
            True(src.CanSeek);
            False(src.CanWrite);
            Throws<NotSupportedException>(() => src.Write(new byte[2], 0, 2));
            Throws<NotSupportedException>(() => src.Write(new byte[2]));
            Throws<NotSupportedException>(() => src.WriteByte(42));
            await ThrowsAsync<NotSupportedException>(src.WriteAsync(new ReadOnlyMemory<byte>()).AsTask);
            await ThrowsAsync<NotSupportedException>(() => src.WriteAsync(new byte[2], 0, 2));
        }
    }
}