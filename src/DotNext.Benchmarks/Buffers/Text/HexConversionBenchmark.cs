using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Code;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using static System.Globalization.CultureInfo;

namespace DotNext.Buffers.Text;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.Declared)]
public class HexConversionBenchmark
{
    public readonly struct ByteArrayParam : IParam
    {
        public readonly byte[] Value;

        internal ByteArrayParam(byte[] bytes) => Value = bytes;

        object IParam.Value => Value;

        string IParam.DisplayText => ToString();

        string IParam.ToSourceCode() => $"new byte[{Value.LongLength}];";

        public override string ToString() => $"{Value.LongLength.ToString(InvariantCulture)} bytes";
    }

    [ParamsSource(nameof(RandomArrays))]
    public ByteArrayParam Bytes;

    public static IEnumerable<ByteArrayParam> RandomArrays
    {
        get
        {
            byte[] bytes;
            Random.Shared.NextBytes(bytes = new byte[16]);
            yield return new ByteArrayParam(bytes);
            Random.Shared.NextBytes(bytes = new byte[64]);
            yield return new ByteArrayParam(bytes);
            Random.Shared.NextBytes(bytes = new byte[128]);
            yield return new ByteArrayParam(bytes);
            Random.Shared.NextBytes(bytes = new byte[256]);
            yield return new ByteArrayParam(bytes);
            Random.Shared.NextBytes(bytes = new byte[512]);
            yield return new ByteArrayParam(bytes);
            Random.Shared.NextBytes(bytes = new byte[1024]);
            yield return new ByteArrayParam(bytes);
        }
    }

    [Benchmark(Description = "Convert.ToHexString", Baseline = true)]
    public string ToHexUsingDotNetConverter() => Convert.ToHexString(Bytes.Value);

    [Benchmark(Description = "Hex.EncodeToUtf16")]
    public string ToUtf16HexUsingHexConverter() => Hex.EncodeToUtf16(Bytes.Value);

    [Benchmark(Description = "Hex.EncodeToUtf8")]
    public byte[] ToUtf8HexUsingHexConverter() => Hex.EncodeToUtf8(Bytes.Value);
}