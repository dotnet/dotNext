using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Globalization.CultureInfo;

namespace DotNext.IO;

using Text;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
public class DictionarySerializationBenchmark
{
    private const int DictionarySize = 100;
    private readonly Dictionary<string, string> data = new(DictionarySize);
    private readonly byte[] buffer = new byte[512];

    [GlobalSetup]
    public void Setup()
    {
        const string value = "Hello, world!";
        for (var i = 0; i < DictionarySize; i++)
            data.Add(i.ToString(InvariantCulture), value);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        data.Clear();
    }

    [Benchmark]
    public async Task SerializeToJsonMemoryStream()
    {
        await using var output = new MemoryStream(1024);
        await JsonSerializer.SerializeAsync(output, data);
    }

    [Benchmark]
    public async Task SerializeToBinaryFormMemoryStream()
    {
        await using var output = new MemoryStream(1024);
        var writer = IAsyncBinaryWriter.Create(output, buffer);
        await writer.WriteInt32Async(data.Count, true);

        var context = new EncodingContext(Encoding.UTF8, true);
        foreach (var (key, value) in data)
        {
            await writer.WriteStringAsync(key.AsMemory(), context, LengthFormat.Plain);
            await writer.WriteStringAsync(value.AsMemory(), context, LengthFormat.Plain);
        }
    }

    [Benchmark]
    public async Task SerializeToJsonFileStream()
    {
        await using var output = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024, FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        await JsonSerializer.SerializeAsync(output, data);
        await output.FlushAsync();
    }

    [Benchmark]
    public async Task SerializeToBinaryFormFileStream()
    {
        await using var output = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024, FileOptions.SequentialScan | FileOptions.Asynchronous | FileOptions.DeleteOnClose);
        var writer = IAsyncBinaryWriter.Create(output, buffer);
        await writer.WriteInt32Async(data.Count, true);

        var context = new EncodingContext(Encoding.UTF8, true);
        foreach (var (key, value) in data)
        {
            await writer.WriteStringAsync(key.AsMemory(), context, LengthFormat.Plain);
            await writer.WriteStringAsync(value.AsMemory(), context, LengthFormat.Plain);
        }

        await output.FlushAsync();
    }
}