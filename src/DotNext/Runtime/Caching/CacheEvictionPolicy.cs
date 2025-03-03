namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents cache eviction policy.
/// </summary>
[Obsolete("Use RandomAccessCache from DotNext.Threading library instead.")]
public enum CacheEvictionPolicy
{
    /// <summary>
    /// Represents Least Recently Used replacement policy.
    /// </summary>
    LRU = 0,

    /// <summary>
    /// Represents Least Frequently Used replacement policy.
    /// </summary>
    LFU,
}