using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Embedding
{
    /// <summary>
    /// Allows to configure Raft-related stuff and turns
    /// the web application into cluster member.
    /// </summary>
    /// <remarks>
    /// Raft-related endpoint handler is embedded into
    /// request processing pipeline of existing application.
    /// </remarks>
    [CLSCompliant(false)]
    public static class ConfigurationExtensions
    {
        /// <summary>
        /// Allows to inject <see cref="ICluster"/>, <see cref="IRaftCluster"/>, <see cref="IExpandableCluster"/>
        /// to application services and establishes network communication with other cluster member.
        /// </summary>
        /// <param name="services">The registry of services.</param>
        /// <param name="memberConfig">The configuration of cluster member.</param>
        /// <returns>The collection of injectable services.</returns>
        public static IServiceCollection BecomeClusterMember(this IServiceCollection services,
            IConfiguration memberConfig)
            => services.AddClusterAsSingleton<RaftHttpCluster>(memberConfig);

        /// <summary>
        /// Setup Raft protocol handler as middleware for the specified application.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <returns>The configured application builder.</returns>
        public static IApplicationBuilder ConsensusProtocolHandler(this IApplicationBuilder builder)
            => builder.Use(RaftProtocolMiddleware.Create);
    }
}
