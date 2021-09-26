using System.Net.NetworkInformation;

namespace DotNext.Net.NetworkInformation;

/// <summary>
/// Describes options for MTU discovery.
/// </summary>
public class MtuDiscoveryOptions : PingOptions
{
    private const int DefaultMinMtuSize = 60;
    private const int DefaultMaxMtuSize = 65500;

    private int minMtuSize, maxMtuSize;

    /// <summary>
    /// Initializes a new discovery options.
    /// </summary>
    /// <param name="ttl">The number of times that ICMP packet can be forwarded by hosts in the route.</param>
    /// <param name="minMtuSize">The lowest possible size of MTU.</param>
    /// <param name="maxMtuSize">The highest possible size of MTU.</param>
    public MtuDiscoveryOptions(byte ttl = 64, int minMtuSize = DefaultMinMtuSize, int maxMtuSize = DefaultMaxMtuSize)
        : base(ttl, true)
    {
        this.minMtuSize = minMtuSize > 0 ? minMtuSize : throw new ArgumentOutOfRangeException(nameof(minMtuSize));
        this.maxMtuSize = maxMtuSize <= DefaultMaxMtuSize ? maxMtuSize : throw new ArgumentOutOfRangeException(nameof(maxMtuSize));
    }

    /// <summary>
    /// Gets or sets the lowest possible size of MTU.
    /// </summary>
    /// <value>The lowest possible size of MTU.</value>
    public int MinMtuSize
    {
        get => minMtuSize;
        set => minMtuSize = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the highest possible size of MTU.
    /// </summary>
    /// <value>The highest possible size of MTU.</value>
    public int MaxMtuSize
    {
        get => maxMtuSize;
        set => maxMtuSize = value <= DefaultMaxMtuSize ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}