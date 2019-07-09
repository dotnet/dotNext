using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    /// <summary>
    /// Allows to route incoming HTTP request to the cluster leader.
    /// </summary>
    [CLSCompliant(false)]
    public static class LeaderRouter
    {
        private sealed class RedirectionMiddleware
        {
            private readonly RequestDelegate next;
            private readonly RaftHttpCluster cluster;

            internal RedirectionMiddleware(RaftHttpCluster cluster, RequestDelegate next)
            {
                this.cluster = cluster;
                this.next = next;
            }

            internal Task Redirect(HttpContext context)
            {
                var leader = cluster.Leader;
                if (leader is null)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }
                else if (leader.IsRemote)
                {
                    var newAddress = new UriBuilder(context.Request.GetEncodedUrl()).SetHostAndPort(leader.Endpoint).Uri.AbsoluteUri;
                    context.Response.Redirect(newAddress, false);
                }
                else
                    return next(context);
                return Task.CompletedTask;
            }
        }

        private static RequestDelegate CreateRedirectionMiddleware(this RaftHttpCluster cluster, RequestDelegate next)
            => new RedirectionMiddleware(cluster, next).Redirect;

        /// <summary>
        /// Defines that the relative path should be handled by a leader node only.
        /// </summary>
        /// <remarks>
        /// If the current node is not the leader then request will be
        /// redirected automatically to the leader node with 302 (Moved Permanently).
        /// If there are no consensus then the request will be failed with  503 (Service Unavailable).
        /// </remarks>
        /// <param name="builder">The request processing pipeline builder.</param>
        /// <param name="path">The path that a leader must handle.</param>
        /// <returns>The request processing pipeline builder.</returns>
        public static IApplicationBuilder RedirectToLeader(this IApplicationBuilder builder, PathString path)
        {
            var cluster = builder.ApplicationServices.GetRequiredService<RaftHttpCluster>();
            return builder.Map(path, pathBuilder => pathBuilder.Use(cluster.CreateRedirectionMiddleware));
        }
    }
}