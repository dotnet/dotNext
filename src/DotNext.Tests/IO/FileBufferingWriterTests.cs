using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
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

        [Fact]
        public static void PermanentFile()
        {
            var expected = RandomBytes(500);
            string fileName;
            using (var writer = new FileBufferingWriter(new FileBufferingWriter.Options { MemoryThreshold = 100, AsyncIO = false, FileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName())}))
            {
                writer.Write(expected);
                False(writer.TryGetWrittenContent(out _, out fileName));
            }

            using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            var actual = new byte[expected.Length];
            fs.ReadBlock(actual);
            Equal(expected, actual);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void ReadWriteWithInitialCapacity(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, initialCapacity: 5, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = writer.GetWrittenContent();
            Equal(bytes, manager.Memory.ToArray());
            if (writer.TryGetWrittenContent(out var content, out var fileName))
            {
                Equal(bytes, content.ToArray());
            }
            else
            {
                NotEmpty(fileName);
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

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void DrainToSpan(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            var buffer = new byte[100];
            Equal(buffer.Length, writer.CopyTo(buffer));
            Equal(bytes[0..100], buffer);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task DrainToMemoryAsync(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, byte.MaxValue);
            writer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            var buffer = new byte[100];
            Equal(buffer.Length, await writer.CopyToAsync(buffer));
            Equal(bytes[0..100], buffer);
        }

        [Fact]
        public static void CtorExceptions()
        {
            Throws<ArgumentOutOfRangeException>(() => new FileBufferingWriter(memoryThreshold : -1));
            var tempFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Throws<DirectoryNotFoundException>(() => new FileBufferingWriter(tempDir: tempFolder));
            Throws<ArgumentOutOfRangeException>(() => new FileBufferingWriter(memoryThreshold: 100, initialCapacity: 101));
            Throws<ArgumentOutOfRangeException>(() => new FileBufferingWriter(initialCapacity: -1));
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

        [Fact]
        public static void EmptyContent()
        {
            using var writer = new FileBufferingWriter();
            True(writer.TryGetWrittenContent(out var content));
            True(content.IsEmpty);
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public static void StressTest(int threshold)
        {
            var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            DictionarySerializer.Serialize(dict, writer);
            using var manager = writer.GetWrittenContent();
            using var source = StreamSource.AsStream(manager.Memory);
            Equal(dict, DictionarySerializer.Deserialize(source));
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public static void StressTest2(int threshold)
        {
            var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            DictionarySerializer.Serialize(dict, writer);
            using var source = new MemoryStream(1024);
            writer.CopyTo(source);
            source.Position = 0L;
            Equal(dict, DictionarySerializer.Deserialize(source));
        }

        [Fact]
        public static void StressTest3()
        {
            var buffer = RandomBytes(1024 * 1024 * 10);    // 10 MB
            using var writer = new FileBufferingWriter(asyncIO: false);
            writer.Write(buffer);
            False(writer.TryGetWrittenContent(out _));
            using var content = writer.GetWrittenContent();
            True(buffer.AsSpan().SequenceEqual(content.Memory.Span));
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public static void StressTest4(int threshold)
        {
            var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            DictionarySerializer.Serialize(dict, writer);
            using var source = writer.GetWrittenContentAsStream();
            Equal(dict, DictionarySerializer.Deserialize(source));
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(10000)]
        public static async Task StressTest4Async(int threshold)
        {
            var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            await JsonSerializer.SerializeAsync(writer, dict);
            await using var source = await writer.GetWrittenContentAsStreamAsync();
            Equal(dict, await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(source));
        }

        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        public static void BufferedReadWrite(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            IBufferWriter<byte> buffer = writer;
            buffer.Write(new ReadOnlySpan<byte>(bytes, 0, byte.MaxValue));
            buffer.Write(bytes.AsSpan(byte.MaxValue));
            Equal(bytes.Length, writer.Length);
            using var manager = writer.GetWrittenContent();
            Equal(bytes, manager.Memory.ToArray());
            if (writer.TryGetWrittenContent(out var content))
            {
                Equal(bytes, content.ToArray());
            }
        }

        [Fact]
        public static void NotEnoughMemory()
        {
            using var writer = new FileBufferingWriter(memoryThreshold: 10, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            IBufferWriter<byte> buffer = writer;
            Throws<InsufficientMemoryException>(() => buffer.Write(bytes));
        }

        private sealed class CallbackChecker : TaskCompletionSource<bool>
        {

            internal void DoCallback(IAsyncResult ar) => SetResult(true);
        }

        [Theory]
        [InlineData(10, false)]
        [InlineData(100, false)]
        [InlineData(1000, false)]
        [InlineData(10, true)]
        [InlineData(100, true)]
        [InlineData(1000, true)]
        public static void ReadWriteApm(int threshold, bool asyncIO)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: asyncIO);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;
            
            var ar = writer.BeginWrite(bytes, 0, byte.MaxValue, null, "state1");
            Equal("state1", ar.AsyncState);
            True(ar.AsyncWaitHandle.WaitOne(DefaultTimeout));
            writer.EndWrite(ar);

            ar = writer.BeginWrite(bytes, byte.MaxValue, bytes.Length - byte.MaxValue, null, "state2");
            Equal("state2", ar.AsyncState);
            True(ar.AsyncWaitHandle.WaitOne(DefaultTimeout));
            writer.EndWrite(ar);

            Equal(bytes.Length, writer.Length);
            using var manager = writer.GetWrittenContent();
            Equal(bytes, manager.Memory.ToArray());
        }

        [Theory]
        [InlineData(10, false)]
        [InlineData(100, false)]
        [InlineData(1000, false)]
        [InlineData(10, true)]
        [InlineData(100, true)]
        [InlineData(1000, true)]
        public static async Task ReadWriteApm2(int threshold, bool asyncIO)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: asyncIO);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;
            
            var checker = new CallbackChecker();
            var ar = writer.BeginWrite(bytes, 0, byte.MaxValue, checker.DoCallback, "state1");
            Equal("state1", ar.AsyncState);
            True(await checker.Task);
            writer.EndWrite(ar);

            checker = new CallbackChecker();
            ar = writer.BeginWrite(bytes, byte.MaxValue, bytes.Length - byte.MaxValue, checker.DoCallback, "state2");
            Equal("state2", ar.AsyncState);
            True(await checker.Task);
            writer.EndWrite(ar);

            Equal(bytes.Length, writer.Length);
            using var manager = await writer.GetWrittenContentAsync();
            Equal(bytes, manager.Memory.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static void CompatWithReadOnlySequence(int threshold)
        {
            using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: false);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            writer.Write(bytes, 0, 450);
            writer.Write(bytes.AsSpan(450));
            Equal(bytes.Length, writer.Length);
            using var source = writer.GetWrittenContent(10);
            Equal(bytes, source.Sequence.ToArray());
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public static async Task CompatWithReadOnlySequenceAsync(int threshold)
        {
            await using var writer = new FileBufferingWriter(memoryThreshold: threshold, asyncIO: true);
            var bytes = new byte[500];
            for (byte i = 0; i < byte.MaxValue; i++)
                bytes[i] = i;

            await writer.WriteAsync(bytes, 0, 450);
            await writer.WriteAsync(bytes.AsMemory(450));
            Equal(bytes.Length, writer.Length);
            using var source = await writer.GetWrittenContentAsync(10);
            Equal(bytes, source.Sequence.ToArray());
        }
    }
}