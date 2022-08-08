using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;

/// <summary>
/// Allows to setup special service used for configuration of <see cref="IRaftCluster"/> instance.
/// </summary>
[CLSCompliant(false)]
public static class RaftClusterConfiguration
{
    /// <summary>
    /// Registers configurator of <see cref="ICluster"/> service registered as a service
    /// in DI container.
    /// </summary>
    /// <typeparam name="TConfig">The type implementing <see cref="IClusterMemberLifetime"/>.</typeparam>
    /// <param name="services">A collection of services provided by DI container.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection ConfigureCluster<TConfig>(this IServiceCollection services)
        where TConfig : class, IClusterMemberLifetime
        => services.AddSingleton<IClusterMemberLifetime, TConfig>();

    /// <summary>
    /// Registers custom persistence engine for the Write-Ahead Log based on <see cref="PersistentState"/> class.
    /// </summary>
    /// <remarks>
    /// If background compaction is configured for WAL then you can implement
    /// <see cref="IO.Log.ILogCompactionSupport"/> interface to provide custom logic for log compaction.
    /// </remarks>
    /// <typeparam name="TPersistentState">The type representing custom persistence engine.</typeparam>
    /// <param name="services">A collection of services provided by DI container.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UsePersistenceEngine<TPersistentState>(this IServiceCollection services)
        where TPersistentState : PersistentState
    {
        Func<IServiceProvider, TPersistentState> engineCast = ServiceProviderServiceExtensions.GetRequiredService<TPersistentState>;

        return services.AddSingleton<TPersistentState>()
            .AddSingleton<IPersistentState>(engineCast)
            .AddSingleton<PersistentState>(engineCast)
            .AddSingleton<IAuditTrail<IRaftLogEntry>>(engineCast)
            .AddSingleton<IHostedService, BackgroundCompactionService>();
    }

    /// <summary>
    /// Registers custom persistence engine for the Write-Ahead Log based on <see cref="PersistentState"/> class.
    /// </summary>
    /// <remarks>
    /// If background compaction is configured for WAL then you can implement
    /// <see cref="IO.Log.ILogCompactionSupport"/> interface to provide custom logic for log compaction.
    /// </remarks>
    /// <typeparam name="TEngine">An interface used for interaction with the persistence engine.</typeparam>
    /// <typeparam name="TImplementation">The type representing custom persistence engine.</typeparam>
    /// <param name="services">A collection of services provided by DI container.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UsePersistenceEngine<TEngine, TImplementation>(this IServiceCollection services)
        where TEngine : class
        where TImplementation : PersistentState, TEngine
    {
        Func<IServiceProvider, TImplementation> engineCast = ServiceProviderServiceExtensions.GetRequiredService<TImplementation>;

        return services.AddSingleton<TImplementation>()
            .AddSingleton<TEngine>(engineCast)
            .AddSingleton<IPersistentState>(engineCast)
            .AddSingleton<PersistentState>(engineCast)
            .AddSingleton<IAuditTrail<IRaftLogEntry>>(engineCast)
            .AddSingleton<IHostedService, BackgroundCompactionService>();
    }

    /// <summary>
    /// Registers a service responsible for maintaining a list of cluster members.
    /// </summary>
    /// <typeparam name="TStorage">The type of the storage service.</typeparam>
    /// <param name="services">A collection of services.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UseConfigurationStorage<TStorage>(this IServiceCollection services)
        where TStorage : class, IClusterConfigurationStorage<UriEndPoint>
        => services.AddSingleton<IClusterConfigurationStorage<UriEndPoint>, TStorage>();

    private static PersistentClusterConfigurationStorage CreatePersistentStorageFromPath(this string path, IServiceProvider services)
        => new(path);

    /// <summary>
    /// Registers persistent storage service for maintaining a list of cluster members.
    /// </summary>
    /// <param name="services">A collection of services.</param>
    /// <param name="path">The absolute path to the folder on the local machine to store the list.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UsePersistentConfigurationStorage(this IServiceCollection services, string path)
        => services.AddSingleton<IClusterConfigurationStorage<UriEndPoint>>(path.CreatePersistentStorageFromPath);

    private static InMemoryClusterConfigurationStorage CreateInMemoryStorage(this Action<IDictionary<ClusterMemberId, UriEndPoint>> configuration, IServiceProvider services)
    {
        var storage = new InMemoryClusterConfigurationStorage();
        var builder = storage.CreateActiveConfigurationBuilder();
        configuration(builder);
        builder.Build();
        return storage;
    }

    /// <summary>
    /// Registers in-memory storage service for maintaining a list of cluster members.
    /// </summary>
    /// <remarks>
    /// In-memory storage is not recommended for production use.
    /// </remarks>
    /// <param name="services">A collection of services.</param>
    /// <param name="configuration">The delegate that allows to configure a list of cluster members at startup.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UseInMemoryConfigurationStorage(this IServiceCollection services, Action<IDictionary<ClusterMemberId, UriEndPoint>> configuration)
        => services.AddSingleton<IClusterConfigurationStorage<UriEndPoint>>(configuration.CreateInMemoryStorage);
}