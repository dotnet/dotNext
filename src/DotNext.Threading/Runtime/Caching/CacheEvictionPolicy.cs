namespace DotNext.Runtime.Caching;

/// <summary>
/// Represents cache eviction policy.
/// </summary>
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