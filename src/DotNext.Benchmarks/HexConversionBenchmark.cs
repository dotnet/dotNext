using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Code;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Order;
using System;
using System.Collections.Generic;
using static System.Globalization.CultureInfo;

namespace DotNext;

[SimpleJob(runStrategy: RunStrategy.Throughput, launchCount: 1)]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
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
        }
    }

    [Benchmark]
    public string ToHexUsingBitConverter() => BitConverter.ToString(Bytes.Value);

    [Benchmark]
    public string ToHexUsingSpanConverter() => Span.ToHex(Bytes.Value);
}