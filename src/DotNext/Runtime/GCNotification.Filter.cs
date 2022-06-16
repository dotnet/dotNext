using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime;

public partial class GCNotification
{
    private protected GCNotification()
    {
    }

    internal abstract bool Test(in GCMemoryInfo info);

    /// <summary>
    /// Combines two filters using logical AND.
    /// </summary>
    /// <param name="right">The filter to be combined with this filter.</param>
    /// <returns>A new filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="right"/> is <see langword="null"/>.</exception>
    public virtual GCNotification And(GCNotification right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return new AndFilter(this, right);
    }

    /// <summary>
    /// Combines two filters using logical OR.
    /// </summary>
    /// <param name="right">The filter to be combined with this filter.</param>
    /// <returns>A new filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="right"/> is <see langword="null"/>.</exception>
    public virtual GCNotification Or(GCNotification right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return new GCOrFilter(this, right);
    }

    /// <summary>
    /// Negates this filter.
    /// </summary>
    /// <returns>A new filter.</returns>
    public virtual GCNotification Negate() => new GCNotFilter(this);

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
    public static GCNotification MemoryThreshold(double threshold)
    {
        if (!double.IsFinite(threshold) || !threshold.IsBetween(0D, 1D, BoundType.RightClosed))
            throw new ArgumentOutOfRangeException(nameof(threshold));

        return new MemoryThresholdFilter(threshold);
    }

    /// <summary>
    /// Combines two filters using logical AND.
    /// </summary>
    /// <param name="left">The first filter to combine.</param>
    /// <param name="right">The second filter to combine.</param>
    /// <returns>A new filter.</returns>
    public static GCNotification operator &(GCNotification left, GCNotification right)
        => left.And(right);

    /// <summary>
    /// Combines two filters using logical OR.
    /// </summary>
    /// <param name="left">The first filter to combine.</param>
    /// <param name="right">The second filter to combine.</param>
    /// <returns>A new filter.</returns>
    public static GCNotification operator |(GCNotification left, GCNotification right)
        => left.Or(right);

    /// <summary>
    /// Negates the filter.
    /// </summary>
    /// <param name="filter">The filter to negate.</param>
    /// <returns>A new filter.</returns>
    public static GCNotification operator !(GCNotification filter) => filter.Negate();

    private sealed class MemoryThresholdFilter : GCNotification
    {
        private readonly double threshold;

        internal MemoryThresholdFilter(double threshold)
        {
            Debug.Assert(double.IsNormal(threshold) && threshold is > 0D and <= 1D);

            this.threshold = threshold;
        }

        internal override bool Test(in GCMemoryInfo info)
            => info.MemoryLoadBytes <= info.HighMemoryLoadThresholdBytes * threshold;
    }

    private sealed class HeapCompactionFilter : GCNotification
    {
        internal static readonly HeapCompactionFilter Instance = new();

        private HeapCompactionFilter()
        {
        }

        internal override bool Test(in GCMemoryInfo info)
            => info.Compacted;
    }

    private sealed class GCEvent : GCNotification
    {
        internal static readonly GCEvent Instance = new();

        private GCEvent()
        {
        }

        internal override bool Test(in GCMemoryInfo info) => true;

        public override GCNotification And(GCNotification right)
            => right;

        public override GCNotification Or(GCNotification right)
            => this;
    }

    private sealed class GenerationFilter : GCNotification
    {
        private readonly int generation;

        internal GenerationFilter(int generation)
        {
            Debug.Assert(generation >= 0 && generation <= GC.MaxGeneration);

            this.generation = generation;
        }

        internal override bool Test(in GCMemoryInfo info)
            => info.Generation == generation;
    }

    private sealed class HeapFragmentationThresholdFilter : GCNotification
    {
        private readonly double fragmentationPercentage;

        internal HeapFragmentationThresholdFilter(double fragmentation)
        {
            Debug.Assert(double.IsFinite(fragmentation) && fragmentation is >= 0D and <= 1D);

            fragmentationPercentage = fragmentation;
        }

        internal override bool Test(in GCMemoryInfo info)
            => ((double)info.FragmentedBytes / info.HeapSizeBytes) >= fragmentationPercentage;
    }

    private sealed class AndFilter : GCNotification
    {
        private readonly GCNotification left, right;

        internal AndFilter(GCNotification left, GCNotification right)
        {
            Debug.Assert(left is not null && right is not null);

            this.left = left;
            this.right = right;
        }

        internal override bool Test(in GCMemoryInfo info)
            => left.Test(in info) && right.Test(in info);
    }

    private sealed class GCOrFilter : GCNotification
    {
        private readonly GCNotification left, right;

        internal GCOrFilter(GCNotification left, GCNotification right)
        {
            Debug.Assert(left is not null && right is not null);

            this.left = left;
            this.right = right;
        }

        internal override bool Test(in GCMemoryInfo info)
            => left.Test(in info) || right.Test(in info);
    }

    private sealed class GCNotFilter : GCNotification
    {
        private readonly GCNotification filter;

        internal GCNotFilter(GCNotification filter)
        {
            Debug.Assert(filter is not null);

            this.filter = filter;
        }

        internal override bool Test(in GCMemoryInfo info) => !filter.Test(in info);

        public override GCNotification Negate()
            => filter;
    }
}