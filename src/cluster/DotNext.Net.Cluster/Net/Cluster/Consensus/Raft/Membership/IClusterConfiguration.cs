namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using IO;

/// <summary>
/// Represents a snapshot of cluster configuration.
/// </summary>
/// <typeparam name="TAddress">The type of the member address.</typeparam>
public interface IClusterConfiguration<TAddress> : IDataTransferObject
    where TAddress : notnull
{
    /// <summary>
    /// Gets a collection of members in the cluster configuration.
    /// </summary>
    IReadOnlySet<TAddress> Members { get; }
    
    /// <summary>
    /// Adds a new member.
    /// </summary>
    /// <param name="address">The address of the member to add.</param>
    /// <returns>A new version of the configuration.</returns>
    IClusterConfiguration<TAddress> Add(TAddress address);

    /// <summary>
    /// Removes the configuration member.
    /// </summary>
    /// <param name="address">The address of the member to remove.</param>
    /// <returns>A new version of the configuration.</returns>
    IClusterConfiguration<TAddress> Remove(TAddress address);
    
    /// <summary>
    /// Removes the address from the configuration.
    /// </summary>
    /// <param name="configuration">The configuration to mutate.</param>
    /// <param name="address">The address to remove.</param>
    /// <returns><see langword="true"/> if the address is removed successfully and <paramref name="configuration"/> is mutated;
    /// otherwise, <see langword="false"/>.</returns>
    public static bool TryRemove(ref IClusterConfiguration<TAddress> configuration, TAddress address)
    {
        var oldConfig = configuration;
        var newConfig = configuration.Remove(address);
        if (ReferenceEquals(newConfig, oldConfig))
            return false;

        configuration = newConfig;
        return true;
    }
    
    internal static bool TryAdd(ref IClusterConfiguration<TAddress> configuration, TAddress address)
    {
        var oldConfig = configuration;
        var newConfig = configuration.Add(address);
        if (ReferenceEquals(newConfig, oldConfig))
            return false;

        configuration = newConfig;
        return true;
    }
}