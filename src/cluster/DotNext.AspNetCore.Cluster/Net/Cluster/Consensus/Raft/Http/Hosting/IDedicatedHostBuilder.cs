using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    /// <summary>
    /// Allows to configure separated web host for Raft.
    /// </summary>
    [CLSCompliant(false)]
    public interface IDedicatedHostBuilder
    {
        /// <summary>
        /// Configures web host dedicated for Raft.
        /// </summary>
        /// <param name="builder">The host builder.</param>
        void Configure(IWebHostBuilder builder);

        /// <summary>
        /// Configures application dedicated for Raft 
        /// </summary>
        /// <param name="builder">The application builder.</param>
        void Configure(IApplicationBuilder builder);
    }

    internal sealed class ClusterMemberHostBuilder
    {
        private readonly IDedicatedHostBuilder? builder;

        internal ClusterMemberHostBuilder(IDedicatedHostBuilder? builder)
            => this.builder = builder;

        internal void Configure(IWebHostBuilder webHost, RaftHostedClusterMemberConfiguration config)
        {
            if (builder is null)
                webHost.UseKestrel(config.ConfigureKestrel);
            else
                builder.Configure(webHost);
        }

        internal void Configure(IApplicationBuilder builder)
            => this.builder?.Configure(builder);
    }
}
