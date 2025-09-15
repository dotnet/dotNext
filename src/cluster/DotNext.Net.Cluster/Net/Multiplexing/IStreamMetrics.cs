using System.Diagnostics.Metrics;

namespace DotNext.Net.Multiplexing;

internal interface IStreamMetrics
{
    static abstract UpDownCounter<long> StreamCount { get; }
}