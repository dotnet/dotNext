using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime;

public partial class GCNotification
{
    private protected GCNotification()
    {
    }

    private protected abstract bool Test(in GCMemoryInfo info);

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

        return new OrFilter(this, right);
    }

    /// <summary>
    /// Combines two filters using logical XOR.
    /// </summary>
    /// <param name="right">The filter to be combined with this filter.</param>
    /// <returns>A new filter.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="right"/> is <see langword="null"/>.</exception>
    public virtual GCNotification ExclusiveOr(GCNotification right)
    {
        ArgumentNullException.ThrowIfNull(right);

        return new XorFilter(this, right);
    }

    /// <summary>
    /// Negates this filter.
    /// </summary>
    /// <returns>A new filter.</returns>
    public virtual GCNotification Negate() => new NotFilter(this);

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
    /// Combines two filters using logical XOR.
    /// </summary>
    /// <param name="left">The first filter to combine.</param>
    /// <param name="right">The second filter to combine.</param>
    /// <returns>A new filter.</returns>
    public static GCNotification operator ^(GCNotification left, GCNotification right)
        => left.ExclusiveOr(right);

    /// <summary>
    /// Negates the filter.
    /// </summary>
    /// <param name="filter">The filter to negate.</param>
    /// <returns>A new filter.</returns>
    public static GCNotification operator !(GCNotification filter) => filter.Negate();

    private sealed class AndFilter : GCNotification
    {
        private readonly GCNotification left, right;

        internal AndFilter(GCNotification left, GCNotification right)
        {
            Debug.Assert(left is not null && right is not null);

            this.left = left;
            this.right = right;
        }

        private protected override bool Test(in GCMemoryInfo info)
            => left.Test(in info) && right.Test(in info);
    }

    private sealed class OrFilter : GCNotification
    {
        private readonly GCNotification left, right;

        internal OrFilter(GCNotification left, GCNotification right)
        {
            Debug.Assert(left is not null && right is not null);

            this.left = left;
            this.right = right;
        }

        private protected override bool Test(in GCMemoryInfo info)
            => left.Test(in info) || right.Test(in info);
    }

    private sealed class XorFilter : GCNotification
    {
        private readonly GCNotification left, right;

        internal XorFilter(GCNotification left, GCNotification right)
        {
            Debug.Assert(left is not null && right is not null);

            this.left = left;
            this.right = right;
        }

        private protected override bool Test(in GCMemoryInfo info)
            => left.Test(in info) ^ right.Test(in info);
    }

    private sealed class NotFilter : GCNotification
    {
        private readonly GCNotification filter;

        internal NotFilter(GCNotification filter)
        {
            Debug.Assert(filter is not null);

            this.filter = filter;
        }

        private protected override bool Test(in GCMemoryInfo info) => !filter.Test(in info);

        public override GCNotification Negate()
            => filter;
    }
}