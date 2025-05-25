using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft;

using IO.Log;
using Membership;
using StateMachine;

/// <summary>
/// Allows setting up the special service used for configuration of <see cref="IRaftCluster"/> instance.
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
    public static IServiceCollection ConfigureCluster<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TConfig>(this IServiceCollection services)
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
    public static IServiceCollection UsePersistenceEngine<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPersistentState>(this IServiceCollection services)
        where TPersistentState : PersistentState
    {
        Func<IServiceProvider, TPersistentState> engineCast = ServiceProviderServiceExtensions.GetRequiredService<TPersistentState>;

        return services.AddSingleton<TPersistentState>()
            .AddSingleton<IPersistentState>(engineCast)
            .AddSingleton<PersistentState>(engineCast)
            .AddSingleton<IAuditTrail<IRaftLogEntry>>(engineCast)
            .AddHostedService<BackgroundCompactionService>();
    }

    /// <summary>
    /// Registers the state machine and the write-ahead log for it.
    /// </summary>
    /// <param name="services">A collection of services provided by DI container.</param>
    /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
    /// <returns>A modified collection of services.</returns>
    [Experimental("DOTNEXT001")]
    public static IServiceCollection UseStateMachine<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStateMachine>(
        this IServiceCollection services)
        where TStateMachine : class, IStateMachine
    {
        Func<IServiceProvider, TStateMachine> stateMachineCast = ServiceProviderServiceExtensions.GetRequiredService<TStateMachine>;
        
        return services.AddSingleton<TStateMachine>()
            .AddSingleton<IStateMachine, TStateMachine>(stateMachineCast)
            .AddSingleton<IPersistentState, WriteAheadLog>();
    }

    /// <summary>
    /// Restores the state machine.
    /// </summary>
    /// <remarks>
    /// The state machine must be registered previously with <see cref="UseStateMachine{TStateMachine}"/> method.
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TStateMachine">The type of the state machine.</typeparam>
    /// <returns>The task representing the state of the asynchronous operation.</returns>
    [Experimental("DOTNEXT001")]
    public static ValueTask RestoreStateAsync<TStateMachine>(this IApplicationBuilder app, CancellationToken token = default)
        where TStateMachine : SimpleStateMachine
    {
        var stateMachine = app.ApplicationServices.GetRequiredService<TStateMachine>();
        return stateMachine.RestoreAsync(token);
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
    public static IServiceCollection UsePersistenceEngine<TEngine, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementation>(this IServiceCollection services)
        where TEngine : class
        where TImplementation : PersistentState, TEngine
    {
        Func<IServiceProvider, TImplementation> engineCast = ServiceProviderServiceExtensions.GetRequiredService<TImplementation>;

        return services.AddSingleton<TImplementation>()
            .AddSingleton<TEngine>(engineCast)
            .AddSingleton<IPersistentState>(engineCast)
            .AddSingleton<PersistentState>(engineCast)
            .AddSingleton<IAuditTrail<IRaftLogEntry>>(engineCast)
            .AddHostedService<BackgroundCompactionService>();
    }

    /// <summary>
    /// Registers a service responsible for maintaining a list of cluster members.
    /// </summary>
    /// <typeparam name="TStorage">The type of the storage service.</typeparam>
    /// <param name="services">A collection of services.</param>
    /// <returns>A modified collection of services.</returns>
    public static IServiceCollection UseConfigurationStorage<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TStorage>(this IServiceCollection services)
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

    private static InMemoryClusterConfigurationStorage CreateInMemoryStorage(this Action<ICollection<UriEndPoint>> configuration, IServiceProvider services)
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
    public static IServiceCollection UseInMemoryConfigurationStorage(this IServiceCollection services, Action<ICollection<UriEndPoint>> configuration)
        => services.AddSingleton<IClusterConfigurationStorage<UriEndPoint>>(configuration.CreateInMemoryStorage);
}