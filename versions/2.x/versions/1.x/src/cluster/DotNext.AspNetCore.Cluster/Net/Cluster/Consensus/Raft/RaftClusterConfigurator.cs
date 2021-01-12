using Microsoft.Extensions.DependencyInjection;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    /// <summary>
    /// Allows to setup special service used for configuration of <see cref="IRaftCluster"/> instance.
    /// </summary>
    [CLSCompliant(false)]
    public static class RaftClusterConfigurator
    {
        /// <summary>
        /// Registers configurator of <see cref="ICluster"/> service registered as a service
        /// in DI container.
        /// </summary>
        /// <typeparam name="TConfig">The type implementing <see cref="IRaftClusterConfigurator"/>.</typeparam>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <returns>A collection of services provided by DI container.</returns>
        public static IServiceCollection ConfigureCluster<TConfig>(this IServiceCollection services)
            where TConfig : class, IRaftClusterConfigurator
            => services.AddSingleton<IRaftClusterConfigurator, TConfig>();
    }
}
