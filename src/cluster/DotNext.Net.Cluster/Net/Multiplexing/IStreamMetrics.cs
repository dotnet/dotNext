using System.Diagnostics;

namespace DotNext.Net.Multiplexing;

internal interface IStreamMetrics
{
    static abstract void ChangeStreamCount(long delta, in TagList measurementTags);
}