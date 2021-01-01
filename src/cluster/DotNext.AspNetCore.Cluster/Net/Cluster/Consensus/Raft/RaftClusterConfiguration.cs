using System;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft
{
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
        /// <typeparam name="TPersistentState">The type representing custom persistence engine.</typeparam>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <returns>A modified collection of services.</returns>
        public static IServiceCollection UsePersistenceEngine<TPersistentState>(this IServiceCollection services)
            where TPersistentState : PersistentState
        {
            Func<IServiceProvider, TPersistentState> engineCast = ServiceProviderServiceExtensions.GetRequiredService<TPersistentState>;

            return services.AddSingleton<TPersistentState>()
                .AddSingleton<IPersistentState>(engineCast);
        }
    }
}
