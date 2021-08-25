using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    /// <summary>
    /// Allows to configure separated web host for Raft.
    /// </summary>
    /// <remarks>
    /// The service implementing this interface should be registered
    /// as singleton service in DI container.
    /// </remarks>
    [Obsolete("Use embedded mode instead")]
    [CLSCompliant(false)]
    public interface IDedicatedHostBuilder : IHostingStartup
    {
        /// <summary>
        /// Configures application dedicated for Raft.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        void Configure(IApplicationBuilder builder);
    }
}
