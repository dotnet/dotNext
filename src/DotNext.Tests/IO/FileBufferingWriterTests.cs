using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    public sealed class FileBufferingWriterTests : Test
    {
        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void ReadWrite(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = writer.GetWrittenContent();
            Equal(bytes, manager.Memory.ToArray());
            if (writer.TryGetWrittenContent(out var content))
            {
                Equal(bytes, content.ToArray());
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task ReadWriteAsync(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, byte.MaxValue);
            await writer.WriteAsync(bytes.AsMemory(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = await writer.GetWrittenContentAsync();
            Equal(bytes, manager.Memory.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task ReuseAfterBuild(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, byte.MaxValue);
            await writer.WriteAsync(bytes.AsMemory(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using (var manager = await writer.GetWrittenContentAsync())
                Equal(bytes, manager.Memory.ToArray());
            await writer.WriteAsync(new byte[] {3, 4, 5}.AsMemory());
            writer.WriteByte(6);
            using (var manager = await writer.GetWrittenContentAsync(500..))
            {
                Equal(new byte[] {3, 4, 5, 6}, manager.Memory.ToArray());
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void ReadWriteBuildWithRange(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = writer.GetWrittenContent(0..255);
            Equal(bytes.AsMemory(0, 255).ToArray(), manager.Memory.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task ReadWriteBuildWithRangeAsync(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, byte.MaxValue);
            await writer.WriteAsync(bytes.AsMemory(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = await writer.GetWrittenContentAsync(0..255);
            Equal(bytes.AsMemory(0, 255).ToArray(), manager.Memory.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void ReuseAfterCleanup(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using (var manager = writer.GetWrittenContent())
                Equal(bytes, manager.Memory.ToArray());

            writer.Clear();
            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using (var manager = writer.GetWrittenContent())
                Equal(bytes, manager.Memory.ToArray());
        }

        [Fact]
        public static async Task UnsupportedMethods()
        {
            await using var writer = new FileBufferingWriter();
            True(writer.CanWrite);
            False(writer.CanRead);
            False(writer.CanSeek);
            False(writer.CanTimeout);
            Throws<NotSupportedException>(() => writer.ReadByte());
            Throws<NotSupportedException>(() => writer.Position);
            Throws<NotSupportedException>(() => writer.Position = 42L);
            Throws<NotSupportedException>(() => writer.SetLength(42L));
            Throws<NotSupportedException>(() => writer.Seek(0L, default));
            Throws<NotSupportedException>(() => writer.Read(new Span<byte>()));
            Throws<NotSupportedException>(() => writer.Read(new byte[10], 0, 10));
            Throws<NotSupportedException>(() => writer.BeginRead(new byte[10], 0, 10, null, null));
            Throws<InvalidOperationException>(() => writer.EndRead(Task.CompletedTask));
            await ThrowsAsync<NotSupportedException>(() => writer.ReadAsync(new byte[10], 0, 10));
            await ThrowsAsync<NotSupportedException>(writer.ReadAsync(new byte[10], CancellationToken.None).AsTask);
        }
        
        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void DrainToStream(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var ms = new MemoryStream(500);
            writer.CopyTo(ms);
            Equal(bytes, ms.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void DrainToBuffer(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            var ms = new ArrayBufferWriter<byte>();
            writer.CopyTo(ms);
            Equal(bytes, ms.WrittenSpan.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task DrainToStreamAsync(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, byte.MaxValue);
            await writer.WriteAsync(bytes.AsMemory(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var ms = new MemoryStream(500);
            await writer.CopyToAsync(ms);
            Equal(bytes, ms.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task DrainToBufferAsync(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, byte.MaxValue);
            await writer.WriteAsync(bytes.AsMemory(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            var ms = new ArrayBufferWriter<byte>();
            await writer.CopyToAsync(ms);
            Equal(bytes, ms.WrittenSpan.ToArray());
        }

        [Fact]
        public static void CtorExceptions()
        {
            Throws<ArgumentOutOfRangeException>(() => new FileBufferingWriter(memoryThreshold : -1));
            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Throws<DirectoryNotFoundException>(() => new FileBufferingWriter(tempDir: tempFolder));
        }

        [Fact]
        public static async Task WriteDuringReadAsync()
        {
            using var writer = new FileBufferingWriter();
            writer.Write(new byte[] {1, 2, 3});
            using var manager = writer.GetWrittenContent();
            Equal(new byte[] {1, 2, 3}, manager.Memory.ToArray());
            Throws<InvalidOperationException>(writer.Clear);
            Throws<InvalidOperationException>(() => writer.WriteByte(2));
            Throws<InvalidOperationException>(() => writer.GetWrittenContent());
            await ThrowsAsync<InvalidOperationException>(() => writer.WriteAsync(new byte[2], 0, 2));
            await ThrowsAsync<InvalidOperationException>(writer.GetWrittenContentAsync().AsTask);
        }
    }
}