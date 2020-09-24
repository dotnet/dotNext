using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DotNext.IO
{
    [SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    public class FileBufferingWriterBenchmark
    {
        private const int MemoryThreshold = 500 * 1024; // 500 KB
        private const int ChunkSize = 1024; //1 KB
        private readonly byte[] content;

        public FileBufferingWriterBenchmark()
        {
            content = new byte[1024 * 1024];    // 1 MB
            new Random().NextBytes(content);
        }

        private IEnumerable<ReadOnlyMemory<byte>> GetChunks()
        {
            var segment = new ReadOnlyMemory<byte>(content);
            var offset = 0;
            const int chunkSize = 1024;

            while (!segment.IsEmpty)
            {
                if (segment.Length > chunkSize)
                {
                    yield return segment.Slice(0, chunkSize);
                    offset += chunkSize;
                }
                else
                {
                    yield return segment;
                    offset += segment.Length;
                }

                segment = segment.Slice(offset);
            }
        }

        [Benchmark]
        public async Task BufferingWriterAsync()
        {
            using var writer = new FileBufferingWriter(memoryThreshold: MemoryThreshold, asyncIO: true);
            foreach (var chunk in GetChunks())
                await writer.WriteAsync(chunk);
            await writer.FlushAsync();

            using var ms = new MemoryStream(content.Length);
            await writer.CopyToAsync(ms);
        }

        [Benchmark]
        public async Task FileBufferWriteStreamFromAspNetCoreAsync()
        {
            using var writer = new FileBufferingWriteStream(memoryThreshold: MemoryThreshold);
            foreach (var chunk in GetChunks())
                await writer.WriteAsync(chunk);
            await writer.FlushAsync();
            
            using var ms = new MemoryStream(content.Length);
            await writer.DrainBufferAsync(ms);
        }

        [Benchmark]
        public void BufferingWriter()
        {
            using var writer = new FileBufferingWriter(memoryThreshold: MemoryThreshold, asyncIO: false);
            foreach (var chunk in GetChunks())
                writer.Write(chunk.Span);
            writer.Flush();

            using var ms = new MemoryStream(content.Length);
            writer.CopyTo(ms);
        }

        [Benchmark]
        public void FileBufferWriteStreamFromAspNetCore()
        {
            using var writer = new FileBufferingWriteStream(memoryThreshold: MemoryThreshold);
            foreach (var chunk in GetChunks())
                writer.Write(chunk.Span);
            writer.Flush();
            
            using var ms = new MemoryStream(content.Length);
            writer.DrainBufferAsync(ms).GetAwaiter().GetResult();
        }
    }
}