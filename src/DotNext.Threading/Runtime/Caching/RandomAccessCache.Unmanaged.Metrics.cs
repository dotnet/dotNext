using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Runtime.Caching;

unsafe partial class RandomAccessCache<TValue>
{
    private const string ValueTypeMeterAttribute = "dotnext.cache.value.type";

    private readonly TagList measurementTags = new()
    {
        { ValueTypeMeterAttribute, typeof(TValue).Name },
    };

    /// <summary>
    /// Sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public TagList MeasurementTags
    {
        init
        {
            value.Add(ValueTypeMeterAttribute, typeof(TValue).Name);
            measurementTags = value;
        }
    }

    private void OnAllocated(Entry* entry)
    {
        UnmanagedCacheMeters.EntriesCount.Add(1L, in measurementTags);
        UnmanagedCacheMeters.AllocatedMemory.Add(entry->Size, in measurementTags);
    }

    // must be called before the entry is released
    private void OnReleased(Entry* entry)
    {
        UnmanagedCacheMeters.EntriesCount.Add(-1L, in measurementTags);
        UnmanagedCacheMeters.AllocatedMemory.Add(-entry->Size, in measurementTags);
    }
}

// The meters are shared by all the instantiations of the generic cache type
file static class UnmanagedCacheMeters
{
    internal static readonly UpDownCounter<long> EntriesCount, AllocatedMemory;

    static UnmanagedCacheMeters()
    {
        var meter = new Meter("DotNext.Runtime.Caching.RandomAccessCache.Unmanaged");
        EntriesCount = meter.CreateUpDownCounter<long>("entries-count", description: "Number of Allocated Entries");
        AllocatedMemory = meter.CreateUpDownCounter<long>("allocated-memory", unit: "B", description: "Allocated Unmanaged Memory");
    }
}
