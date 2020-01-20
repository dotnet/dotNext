namespace DotNext.Threading
{
    /// <summary>
    /// Represents configuration provider for the distributed lock.
    /// </summary>
    /// <param name="lockName">The name of distributed lock.</param>
    /// <returns>The options of distributed lock; or <see langword="null"/> to use default options.</returns>
    public delegate DistributedLockOptions? DistributedLockConfigurationProvider(string lockName);
}
