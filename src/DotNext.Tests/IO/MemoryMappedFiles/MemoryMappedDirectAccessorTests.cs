using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.MemoryMappedFiles;
using Xunit;

namespace DotNext.IO.MemoryMappedFiles
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryMappedDirectAccessorTests : Test
    {
        [Fact]
        public static void EmptyAccessor()
        {
            var da = default(MemoryMappedDirectAccessor);
            True(da.IsEmpty);
            Equal(0L, da.Offset);
            True(da.Pointer.IsNull);
            Equal(0L, da.Size);
            da.Flush();
            Throws<ObjectDisposedException>(da.Clear);
            Equal(Stream.Null, da.AsStream());
        }

        [Fact]
        public static void ClearMemory()
        {
            var tempFile = Path.GetTempFileName();
            using var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.OpenOrCreate, null, 1024, MemoryMappedFileAccess.ReadWrite);
            using var da = mappedFile.CreateDirectAccessor(10L, 4L);
            da.Bytes[0] = 10;
            da.Bytes[1] = 20;
            Equal(10, da.Bytes[0]);
            Equal(20, da.Bytes[1]);
            da.Clear();
            Equal(0, da.Bytes[0]);
            Equal(0, da.Bytes[1]);
        }

        [Fact]
        public static void AccessByPointer()
        {
            var tempFile = Path.GetTempFileName();
            using (var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.OpenOrCreate, null, 1024, MemoryMappedFileAccess.ReadWrite))
            using (var da = mappedFile.CreateDirectAccessor(10L, 4L))
            {
                False(da.IsEmpty);
                Equal(4L, da.Size);
                Equal(10L, da.Offset);
                var ptr = da.Pointer;
                ptr.Value = 1;
                ptr += 1;
                ptr.Value = 2;
                ptr += 1;
                ptr.Value = 3;
                ptr += 1;
                ptr.Value = 5;
                da.Flush();
                using var stream = da.AsStream();
                var data = new byte[4];
                Equal(4, stream.Read(data, 0, data.Length));
                Equal(1, data[0]);
                Equal(2, data[1]);
                Equal(3, data[2]);
                Equal(5, data[3]);
            }
            using (var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read))
            {
                fs.Position = 10;
                var data = new byte[4];
                Equal(4, fs.Read(data, 0, data.Length));
                Equal(1, data[0]);
                Equal(2, data[1]);
                Equal(3, data[2]);
                Equal(5, data[3]);
            }
        }

        [Fact]
        public static void AccessBySpan()
        {
            var tempFile = Path.GetTempFileName();
            using (var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.OpenOrCreate, null, 1024, MemoryMappedFileAccess.ReadWrite))
            using (var da = mappedFile.CreateDirectAccessor(10L, 4L))
            {
                Equal(4L, da.Size);
                var span = da.Bytes;
                Equal(4, span.Length);
                span[0] = 5;
                span[1] = 7;
                span[2] = 12;
                span[3] = 18;
                da.Flush();
            }
            using var fs = new FileStream(tempFile, FileMode.Open, FileAccess.Read)
            {
                Position = 10
            };
            var data = new byte[4];
            Equal(4, fs.Read(data, 0, data.Length));
            Equal(5, data[0]);
            Equal(7, data[1]);
            Equal(12, data[2]);
            Equal(18, data[3]);
        }

        [Fact]
        public static void AccessByStream()
        {
            var tempFile = Path.GetTempFileName();
            using var mappedFile = MemoryMappedFile.CreateFromFile(tempFile, FileMode.OpenOrCreate, null, 1024, MemoryMappedFileAccess.ReadWrite);
            using var da = mappedFile.CreateDirectAccessor(10L, 4L);
            Equal(4L, da.Size);
            var span = da.Bytes;
            Equal(4, span.Length);
            span[0] = 5;
            span[1] = 7;
            span[2] = 12;
            span[3] = 18;
            da.Flush();
            using var mems = da.AsStream();
            var data = new byte[4];
            Equal(4, mems.Read(data, 0, data.Length));
            Equal(5, data[0]);
            Equal(7, data[1]);
            Equal(12, data[2]);
            Equal(18, data[3]);
        }
    }
}