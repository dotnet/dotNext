using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Buffers
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryWriterTests : Test
    {
        private static void WriteReadUsingSpan(MemoryWriter<byte> writer)
        {
            True(writer.WrittenMemory.IsEmpty);
            Equal(0, writer.WrittenCount);

            var span = writer.GetSpan(100);
            new byte[]{10, 20, 30}.AsSpan().CopyTo(span);
            writer.Advance(3);
        
            var result = writer.WrittenMemory.Span;
            Equal(3, writer.WrittenCount);
            Equal(3, result.Length);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            
            span = writer.GetSpan(3);
            new byte[]{40, 50, 60}.AsSpan().CopyTo(span);
            writer.Advance(3);

            result = writer.WrittenMemory.Span;
            Equal(6, writer.WrittenCount);
            Equal(6, result.Length);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            Equal(40, result[3]);
            Equal(50, result[4]);
            Equal(60, result[5]);
        }

        private static void WriteReadUsingMemory(MemoryWriter<byte> writer)
        {
            True(writer.WrittenMemory.IsEmpty);
            Equal(0, writer.WrittenCount);

            var memory = writer.GetMemory(100);
            new byte[]{10, 20, 30}.AsMemory().CopyTo(memory);
            writer.Advance(3);
        
            var result = writer.WrittenMemory.Span;
            Equal(3, result.Length);
            Equal(3, writer.WrittenCount);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            
            memory = writer.GetMemory(3);
            new byte[]{40, 50, 60}.AsMemory().CopyTo(memory);
            writer.Advance(3);

            result = writer.WrittenMemory.Span;
            Equal(6, writer.WrittenCount);
            Equal(6, result.Length);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            Equal(40, result[3]);
            Equal(50, result[4]);
            Equal(60, result[5]);
        }

        [Fact]
        public static void PooledBufferWriterDefaultCapacity()
        {
            var allocator = MemoryPool<byte>.Shared.ToAllocator();
            using(var writer = new PooledBufferWriter<byte>(allocator))
                WriteReadUsingSpan(writer);
            using(var writer = new PooledBufferWriter<byte>(allocator))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void PooledBufferWriterWithCapacity()
        {
            var allocator = MemoryPool<byte>.Shared.ToAllocator();
            Throws<ArgumentOutOfRangeException>(new Action(() => new PooledBufferWriter<byte>(allocator, 0)));
            using(var writer = new PooledBufferWriter<byte>(allocator, 30))
                WriteReadUsingSpan(writer);
            using(var writer = new PooledBufferWriter<byte>(allocator, 20))
                WriteReadUsingMemory(writer);
        }
        
        [Fact]
        public static void PooledArrayBufferWriterDefaultCapacity()
        {
            using(var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared))
                WriteReadUsingSpan(writer);
            using(var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void PooledArrayBufferWriterWithCapacity()
        {
            Throws<ArgumentOutOfRangeException>(new Action(() => new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 0)));
            using(var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 30))
                WriteReadUsingSpan(writer);
            using(var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 20))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void ReadWriteUsingArray()
        {
            using var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 25);
            True(writer.WrittenArray.Count == 0);
            Equal(0, writer.WrittenCount);

            var memory = writer.GetArray(100);
            new ArraySegment<byte>(new byte[]{10, 20, 30}).CopyTo(memory);
            writer.Advance(3);
        
            var result = writer.WrittenArray;
            Equal(3, result.Count);
            Equal(3, writer.WrittenCount);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            
            memory = writer.GetArray(3);
            new ArraySegment<byte>(new byte[]{40, 50, 60}).CopyTo(memory);
            writer.Advance(3);

            result = writer.WrittenArray;
            Equal(6, writer.WrittenCount);
            Equal(6, result.Count);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            Equal(40, result[3]);
            Equal(50, result[4]);
            Equal(60, result[5]);
        }
    }
}