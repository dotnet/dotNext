using Microsoft.AspNetCore.Builder;
using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    /// <summary>
    /// Represents builder of request processing pipeline for
    /// dedicated consensus protocol host.
    /// </summary>
    /// <param name="builder">The builder of request processing pipeline.</param>
    /// <returns>The constructed request processing pipeline.</returns>
    [CLSCompliant(false)]
    public delegate IApplicationBuilder ApplicationBuilder(IApplicationBuilder builder);
}
