using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft
{
    using DistributedServices;

    /// <summary>
    /// Allows to setup special service used for configuration of <see cref="IRaftCluster"/> instance.
    /// </summary>
    [CLSCompliant(false)]
    public static class RaftClusterConfigurator
    {
        private sealed class DistributedApplicationStateFactory
        {
            private readonly string path;
            private readonly int recordsPerPartition;

            internal DistributedApplicationStateFactory(string path, int recordsPerPartition)
            {
                this.path = path;
                this.recordsPerPartition = recordsPerPartition;
            }

            internal DistributedApplicationState CreateState(IServiceProvider dependencies)
            {
                var configuration = dependencies.GetService<IOptions<PersistentState.Options>>();
                return new DistributedApplicationState(path, recordsPerPartition, configuration?.Value);
            }
        }

        /// <summary>
        /// Registers configurator of <see cref="ICluster"/> service registered as a service
        /// in DI container.
        /// </summary>
        /// <typeparam name="TConfig">The type implementing <see cref="IClusterMemberLifetime"/>.</typeparam>
        /// <param name="services">A collection of services provided by DI container.</param>
        /// <returns>A collection of services provided by DI container.</returns>
        public static IServiceCollection ConfigureCluster<TConfig>(this IServiceCollection services)
            where TConfig : class, IClusterMemberLifetime
            => services.AddSingleton<IClusterMemberLifetime, TConfig>();

        /// <summary>
        /// Enables distributed services such as distributed locks.
        /// </summary>
        /// <remarks>
        /// This method allows to use distributed services exposed by singleton
        /// implementation of <see cref="IDistributedApplicationEnvironment"/>.
        /// <paramref name="recordsPerPartition"/> should be a constant that is selected carefully and cannot
        /// be changed between versions of the same application. Otherwise, existing
        /// audit trail cannot be restored correctly. If you need to change this value then
        /// write your own tool for audit trail conversion.
        /// </remarks>
        /// <param name="services">The service registry.</param>
        /// <param name="path">The path to the directory used to maintain persistent state of cluster member.</param>
        /// <param name="recordsPerPartition">The numbers of log entries per partition file.</param>
        /// <returns>The service registry.</returns>
        /// <seealso cref="PersistentState"/>
        /// <seealso cref="DistributedApplicationState"/>
        /// <seealso cref="IDistributedApplicationEnvironment"/>
        public static IServiceCollection EnableDistributedServices(this IServiceCollection services, string path, int recordsPerPartition)
            => services.AddSingleton<IPersistentState, DistributedApplicationState>(new DistributedApplicationStateFactory(path, recordsPerPartition).CreateState);
    }
}
