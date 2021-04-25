using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;
using Enumerable = System.Linq.Enumerable;

namespace DotNext.Buffers
{
    using IO;

    [ExcludeFromCodeCoverage]
    public sealed class MemoryWriterTests : Test
    {
        private static void WriteReadUsingSpan(BufferWriter<byte> writer)
        {
            True(writer.WrittenMemory.IsEmpty);
            Equal(0, writer.WrittenCount);

            var span = writer.GetSpan(100);
            new byte[] { 10, 20, 30 }.AsSpan().CopyTo(span);
            writer.Advance(3);

            var result = writer.WrittenMemory.Span;
            Equal(3, writer.WrittenCount);
            Equal(3, result.Length);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);

            span = writer.GetSpan(3);
            new byte[] { 40, 50, 60 }.AsSpan().CopyTo(span);
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

        private static void WriteReadUsingMemory(BufferWriter<byte> writer)
        {
            True(writer.WrittenMemory.IsEmpty);
            Equal(0, writer.WrittenCount);

            var memory = writer.GetMemory(100);
            new byte[] { 10, 20, 30 }.AsMemory().CopyTo(memory);
            writer.Advance(3);

            var result = writer.WrittenMemory.Span;
            Equal(3, result.Length);
            Equal(3, writer.WrittenCount);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);

            memory = writer.GetMemory(3);
            new byte[] { 40, 50, 60 }.AsMemory().CopyTo(memory);
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
            using (var writer = new PooledBufferWriter<byte>(allocator))
                WriteReadUsingSpan(writer);
            using (var writer = new PooledBufferWriter<byte>(allocator))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void PooledBufferWriterWithCapacity()
        {
            var allocator = MemoryPool<byte>.Shared.ToAllocator();
            Throws<ArgumentOutOfRangeException>(new Action(() => new PooledBufferWriter<byte>(allocator, 0)));
            using (var writer = new PooledBufferWriter<byte>(allocator, 30))
                WriteReadUsingSpan(writer);
            using (var writer = new PooledBufferWriter<byte>(allocator, 20))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void PooledArrayBufferWriterDefaultCapacity()
        {
            using (var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared))
                WriteReadUsingSpan(writer);
            using (var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void PooledArrayBufferWriterWithCapacity()
        {
            Throws<ArgumentOutOfRangeException>(new Action(() => new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 0)));
            using (var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 30))
                WriteReadUsingSpan(writer);
            using (var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 20))
                WriteReadUsingMemory(writer);
        }

        [Fact]
        public static void ReadWriteUsingArray()
        {
            using var writer = new PooledArrayBufferWriter<byte>(ArrayPool<byte>.Shared, 25);
            True(writer.Capacity >= 25);
            True(writer.WrittenArray.Count == 0);
            Equal(0, writer.WrittenCount);

            var memory = writer.GetArray(100);
            new ArraySegment<byte>(new byte[] { 10, 20, 30 }).CopyTo(memory);
            writer.Advance(3);

            var result = writer.WrittenArray;
            Equal(3, result.Count);
            Equal(3, writer.WrittenCount);
            Equal(10, result[0]);
            Equal(20, result[1]);
            Equal(30, result[2]);
            Equal(10, writer[0]);
            Equal(20, writer[1]);
            Equal(30, writer[2]);

            memory = writer.GetArray(3);
            new ArraySegment<byte>(new byte[] { 40, 50, 60 }).CopyTo(memory);
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

        [Fact]
        public static void StressTest()
        {
            var dict = new Dictionary<string, string>
            {
                {"Key1", "Value1"},
                {"Key2", "Value2"}
            };
            using var writer = new PooledArrayBufferWriter<byte>();
            // serialize dictionary to memory
            using (var output = StreamSource.AsStream(writer))
            {
                DictionarySerializer.Serialize(dict, output);
            }
            // deserialize from memory
            using (var input = StreamSource.AsStream(writer.WrittenArray))
            {
                Equal(dict, DictionarySerializer.Deserialize(input));
            }
        }

        [Fact]
        public static void ReuseArrayWriter()
        {
            using var writer = new PooledArrayBufferWriter<byte>();
            var span = writer.GetSpan(10);
            span[0] = 20;
            span[9] = 30;
            writer.Advance(10);
            writer.Clear(true);

            span = writer.GetSpan(10);
            span[0] = 40;
            span[9] = 50;
            writer.Advance(10);

            Equal(40, writer.WrittenMemory.Span[0]);
            Equal(50, writer.WrittenMemory.Span[9]);
        }

        [Fact]
        public static void ReuseMemoryWriter()
        {
            using var writer = new PooledBufferWriter<byte>(MemoryPool<byte>.Shared.ToAllocator());
            Equal(0, writer.Capacity);
            var span = writer.GetSpan(10);
            span[0] = 20;
            span[9] = 30;
            writer.Advance(10);
            writer.Clear(true);

            span = writer.GetSpan(10);
            span[0] = 40;
            span[9] = 50;
            writer.Advance(10);

            Equal(40, writer.WrittenMemory.Span[0]);
            Equal(50, writer.WrittenMemory.Span[9]);
            Equal(40, writer[0]);
            Equal(50, writer[9]);
        }

        [Fact]
        public static void WriterAsReadOnlyCollection()
        {
            using var writer = new PooledArrayBufferWriter<int>();
            IReadOnlyList<int> collection = writer;
            Empty(collection);
            
            writer.Add(42);
            Equal(1, writer.WrittenCount);
            Equal(1, collection.Count);
            Equal(42, collection[0]);
            Throws<IndexOutOfRangeException>(() => collection[1]);
            Equal(42, Enumerable.First(collection));
            Equal(1, Enumerable.Count(collection));

            writer.AddAll(new[] {43, 44});
            Equal(3, writer.WrittenCount);
            Equal(3, collection.Count);
            Equal(42, collection[0]);
            Equal(43, collection[1]);
            Equal(44, collection[2]);
            Throws<IndexOutOfRangeException>(() => collection[3]);
            Equal(3, Enumerable.Count(collection));
            Equal(new[] {42, 43, 44}, Enumerable.ToArray(collection));
        }

        [Fact]
        public static void WriterAsList()
        {
            using var writer = new PooledArrayBufferWriter<int>();
            IList<int> list = writer;
            False(list.IsReadOnly);
            Empty(writer);
            True(list.IndexOf(0) < 0);
            False(list.Contains(0));
            False(list.Remove(0));
            Throws<ArgumentOutOfRangeException>(() => list.RemoveAt(0));

            list.Add(42);
            Equal(1, writer.WrittenCount);
            Equal(1, list.Count);
            Equal(42, list[0]);
            True(list.Contains(42));
            False(list.Contains(0));
            Equal(0, list.IndexOf(42));
            Throws<ArgumentOutOfRangeException>(() => list[1]);
            Equal(42, Enumerable.First(list));
            Equal(1, Enumerable.Count(list));
            list[0] = 10;
            Equal(10, writer.WrittenArray[0]);

            list.Insert(0, 42);
            Equal(2, writer.WrittenCount);
            Equal(2, list.Count);
            True(list.Contains(10));
            True(list.Contains(42));
            Equal(0, list.IndexOf(42));
            Equal(1, list.IndexOf(10));
            Equal(42, list[0]);
            Equal(10, list[1]);

            list.RemoveAt(0);
            Equal(1, writer.WrittenCount);
            Equal(1, list.Count);
            Equal(10, list[0]);
            True(list.Contains(10));
            False(list.Contains(42));

            True(list.Remove(10));
            Empty(list);
            Throws<ArgumentOutOfRangeException>(() => list.Insert(1, 56));

            list.Insert(0, 56);
            Equal(1, writer.WrittenCount);
            Equal(1, list.Count);
            Equal(56, list[0]);

            //check insertion with overflow
            for (var i = writer.Capacity; i > 0 ; i--)
            {
                list.Insert(0, i + 100);
            }
        }

        [Fact]
        public static void Insertion()
        {
            Span<byte> block = stackalloc byte[] { 10, 20, 30 };
            using var writer = new PooledArrayBufferWriter<byte>();

            writer.Insert(0, block);
            Equal(3, writer.WrittenCount);
            Equal(10, writer[0]);
            Equal(20, writer[1]);
            Equal(30, writer[2]);

            block[0] = 40;
            block[1] = 50;
            block[2] = 60;
            writer.Insert(3, block);
            Equal(6, writer.WrittenCount);
            Equal(10, writer[0]);
            Equal(20, writer[1]);
            Equal(30, writer[2]);
            Equal(40, writer[3]);
            Equal(50, writer[4]);
            Equal(60, writer[5]);

            writer.Clear(true);
            var random = RandomBytes(writer.FreeCapacity);
            writer.Write(random);
            Equal(0, writer.FreeCapacity);

            block[0] = 100;
            block[1] = 110;
            block[2] = 120;

            writer.Insert(writer.WrittenCount - 1, block);
            Equal(random[0..(random.Length - 1)], writer.WrittenMemory.Span.Slice(0, random.Length - 1).ToArray());
            Equal(block.ToArray(), writer.WrittenMemory.Span.Slice(random.Length - 1, 3).ToArray());
        }

        [Fact]
        public static void Overwrite()
        {
            Span<byte> block = stackalloc byte[] { 10, 20, 30 };
            using var writer = new PooledArrayBufferWriter<byte>();

            writer.Overwrite(0, block);
            Equal(3, writer.WrittenCount);
            Equal(10, writer[0]);
            Equal(20, writer[1]);
            Equal(30, writer[2]);

            writer.Clear(true);
            var random = RandomBytes(writer.FreeCapacity);
            writer.Write(random);
            Equal(0, writer.FreeCapacity);
            var random2 = RandomBytes(random.Length + 1);
            writer.Overwrite(1, random2);
            Equal(random[0], writer[0]);
            Equal(random2, writer.WrittenMemory.Span.Slice(1).ToArray());
        }

        private sealed class AllocationEventCounter
        {
            internal int Value;

            internal void WriteMetric(int value) => this.Value = value;
        }

        [Fact]
        public static void BufferSizeCallback()
        {
            var counter = new AllocationEventCounter();
            using (var writer = new PooledArrayBufferWriter<byte> { BufferSizeCallback = counter.WriteMetric})
                writer.Write(new byte[] { 1, 2, 3 });
            True(counter.Value >= 3);
        }

        [Fact]
        public static void RemoveTailElements()
        {
            using var writer = new PooledArrayBufferWriter<string>()
            {
                "a",
                "b",
                "c",
            };

            writer.RemoveLast(2);
            Equal(1, writer.WrittenCount);
            Equal("a", writer[0]);
            writer.RemoveLast(1);
            Equal(0, writer.WrittenCount);
            Throws<ArgumentOutOfRangeException>(() => writer.RemoveLast(-1));
        }

        [Fact]
        public static void RemoveHeadElements()
        {
            using var writer = new PooledArrayBufferWriter<string>()
            {
                "a",
                "b",
                "c",
            };

            writer.RemoveFirst(2);
            Equal(1, writer.WrittenCount);
            Equal("c", writer[0]);
            writer.RemoveFirst(1);
            Equal(0, writer.WrittenCount);
            Throws<ArgumentOutOfRangeException>(() => writer.RemoveFirst(-1));
        }
    }
}