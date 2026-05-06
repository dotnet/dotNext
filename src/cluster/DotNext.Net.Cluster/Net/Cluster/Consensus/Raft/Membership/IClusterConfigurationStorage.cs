namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using IO;

/// <summary>
/// Provides a storage of cluster members.
/// </summary>
public interface IClusterConfigurationStorage : IDisposable
{
    /// <summary>
    /// Saves the configuration to the storage.
    /// </summary>
    /// <param name="configuration">The configuration to store.</param>
    /// <param name="configurationVersion">The configuration version.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns><see langword="true"/> if <paramref name="configurationVersion"/> is co</returns>
    ValueTask<bool> SaveConfigurationAsync<TConfiguration>(TConfiguration configuration, long configurationVersion, CancellationToken token = default)
        where TConfiguration : IDataTransferObject;

    /// <summary>
    /// Loads configuration from the storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The copy of the configuration.</returns>
    ValueTask<(IDataTransferObject Configuration, long Version)> LoadConfigurationAsync(CancellationToken token = default);
}

/// <summary>
/// Provides a storage of cluster members.
/// </summary>
/// <typeparam name="TAddress">The type of the cluster member address.</typeparam>
public interface IClusterConfigurationStorage<TAddress> : IClusterConfigurationStorage
    where TAddress : notnull
{
    /// <summary>
    /// Loads configuration from the storage.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The copy of the configuration.</returns>
    new ValueTask<IClusterConfiguration<TAddress>> LoadConfigurationAsync(CancellationToken token = default);
    
    /// <summary>
    /// An event occurred when the configuration is changed.
    /// </summary>
    event Func<IClusterConfiguration<TAddress>, CancellationToken, ValueTask> ConfigurationChanged;
}