using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class RaftProtocolMiddleware
    {
        private readonly RequestDelegate next;

        private RaftProtocolMiddleware(RequestDelegate next) => this.next = next;

        internal static RequestDelegate Create(RequestDelegate next)
            => new RaftProtocolMiddleware(next).FilterRequest;

        private async Task FilterRequest(HttpContext context)
        {
            var cluster = context.RequestServices.GetService<RaftHttpCluster>();
            if (cluster is null || !await cluster.ProcessRequest(context).ConfigureAwait(false))
                await next(context).ConfigureAwait(false);
        }
    }
}
