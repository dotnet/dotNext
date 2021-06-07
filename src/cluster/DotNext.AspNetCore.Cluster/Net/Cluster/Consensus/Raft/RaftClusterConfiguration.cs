using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using IO.Log;

    /// <summary>
    /// Allows to setup special service used for configuration of <see cref="IRaftCluster"/> instance.
    /// </summary>
    [CLSCompliant(false)]
    public static class RaftClusterConfiguration
    {
        /// <summary>
        /// Represents name of configuration options describing <see cref="RaftLogEntryBufferingOptions"/>
        /// instance used for buffering of log entries when transmitting over the wire.
        /// </summary>
        public const string TransportLevelBufferingOptionsName = "RaftBufferingOptions";

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
        /// Registers cluster members discovery service.
        /// </summary>
        /// <typeparam name="TService">The type implementing custom discovery service.</typeparam>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <returns>A modified collection of services.</returns>
        public static IServiceCollection UseDiscoveryService<TService>(this IServiceCollection services)
            where TService : class, IMemberDiscoveryService
            => services.AddSingleton<IMemberDiscoveryService, TService>();

        /// <summary>
        /// Enables buffering of log entries when transferring them over the wire.
        /// </summary>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <param name="options">The delegate used to provide configuration options.</param>
        /// <returns>A modified collection of services.</returns>
        /// <seealso cref="TransportLevelBufferingOptionsName"/>
        [Obsolete("Buffering is no longer needed because persistent WAL supports concurrent reads/writes")]
        public static IServiceCollection EnableBuffering(this IServiceCollection services, Action<RaftLogEntriesBufferingOptions> options)
            => services.Configure(TransportLevelBufferingOptionsName, options);

        internal static RaftLogEntriesBufferingOptions? GetBufferingOptions(this IServiceProvider dependencies)
            => dependencies.GetService<IOptionsMonitor<RaftLogEntriesBufferingOptions>>()?.Get(RaftClusterConfiguration.TransportLevelBufferingOptionsName);
    }
}
