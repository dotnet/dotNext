using System.Diagnostics;

namespace DotNext.Runtime;

using Patterns;

/// <summary>
/// Represents a collection of GC notifications.
/// </summary>
public static class GCExtensions
{
    /// <summary>
    /// Extends <see cref="GC"/> class.
    /// </summary>
    extension(GC)
    {
        /// <summary>
        /// Creates a filter that allows to detect heap compaction.
        /// </summary>
        /// <returns>A new filter.</returns>
        public static GCNotification WhenCompactionOccurred()
            => HeapCompactionFilter.Instance;

        /// <summary>
        /// Creates a filter that triggers notification on every GC occurred.
        /// </summary>
        /// <returns>A new filter.</returns>
        public static GCNotification WhenTriggered()
            => CollectionTriggered.Instance;
        
        /// <summary>
        /// Creates a filter that allows to detect garbage collection of the specified generation.
        /// </summary>
        /// <param name="generation">The expected generation.</param>
        /// <returns>A new filter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="generation"/> is less than 0 or greater than <see cref="GC.MaxGeneration"/>.</exception>
        public static GCNotification WhenTriggered(int generation)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)generation, (uint)GC.MaxGeneration, nameof(generation));

            return new GenerationFilter(generation);
        }
        
        /// <summary>
        /// Creates a filter that allows to detect managed heap fragmentation threshold.
        /// </summary>
        /// <param name="threshold">The memory threshold. The memory threshold; must be in range (0, 1].</param>
        /// <returns>A new filter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> is invalid.</exception>
        public static GCNotification WhenHeapFragmented(double threshold)
        {
            if (!double.IsFinite(threshold) || threshold is <= 0D or > 1D)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            return new HeapFragmentationThresholdFilter(threshold);
        }
        
        /// <summary>
        /// Creates a filter that allows to detect managed heap occupation.
        /// </summary>
        /// <remarks>
        /// This filter allows to detect a specific ratio between <see cref="GCMemoryInfo.MemoryLoadBytes"/>
        /// and <see cref="GCMemoryInfo.HighMemoryLoadThresholdBytes"/>.
        /// </remarks>
        /// <param name="threshold">The memory threshold. The memory threshold; must be in range (0, 1].</param>
        /// <returns>A new filter.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> is invalid.</exception>
        public static GCNotification WhenMemoryPressure(double threshold)
        {
            if (!double.IsFinite(threshold) || threshold is <= 0D or > 1D)
                throw new ArgumentOutOfRangeException(nameof(threshold));

            return new MemoryPressureFilter(threshold);
        }
    }
}

file sealed class MemoryPressureFilter : GCNotification
{
    private readonly double threshold;

    internal MemoryPressureFilter(double threshold)
    {
        Debug.Assert(double.IsNormal(threshold) && threshold is > 0D and <= 1D);

        this.threshold = threshold;
    }

    private protected override bool Test(in GCMemoryInfo info)
        => info.MemoryLoadBytes <= info.HighMemoryLoadThresholdBytes * threshold;
}

file sealed class HeapCompactionFilter : GCNotification, ISingleton<HeapCompactionFilter>
{
    public static HeapCompactionFilter Instance { get; } = new();

    private HeapCompactionFilter()
    {
    }

    private protected override bool Test(in GCMemoryInfo info)
        => info.Compacted;
}

file sealed class CollectionTriggered : GCNotification, ISingleton<CollectionTriggered>
{
    public static CollectionTriggered Instance { get; } = new();

    private CollectionTriggered()
    {
    }

    private protected override bool Test(in GCMemoryInfo info) => true;

    public override GCNotification And(GCNotification right)
        => right;

    public override GCNotification Or(GCNotification right)
        => this;
}

file sealed class GenerationFilter : GCNotification
{
    private readonly int generation;

    internal GenerationFilter(int generation)
    {
        Debug.Assert(generation >= 0 && generation <= GC.MaxGeneration);

        this.generation = generation;
    }

    private protected override bool Test(in GCMemoryInfo info)
        => info.Generation == generation;
}

file sealed class HeapFragmentationThresholdFilter : GCNotification
{
    private readonly double fragmentationPercentage;

    internal HeapFragmentationThresholdFilter(double fragmentation)
    {
        Debug.Assert(double.IsFinite(fragmentation) && fragmentation is >= 0D and <= 1D);

        fragmentationPercentage = fragmentation;
    }

    private protected override bool Test(in GCMemoryInfo info)
        => ((double)info.FragmentedBytes / info.HeapSizeBytes) >= fragmentationPercentage;
}