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
            private readonly int? applicationPortHint;
            private readonly Action<HttpResponse, Uri> redirection;

            internal RedirectionMiddleware(RaftHttpCluster cluster, RequestDelegate next, int? applicationPortHint, Action<HttpResponse, Uri> redirection)
            {
                this.cluster = cluster;
                this.next = next;
                this.applicationPortHint = applicationPortHint;
                this.redirection = redirection ?? Redirect;
            }

            private static void Redirect(HttpResponse response, Uri leaderUri) => response.Redirect(leaderUri.AbsoluteUri, false);

            internal Task Redirect(HttpContext context)
            {
                var leader = cluster.Leader;
                if (leader is null)
                {
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                }
                else if (leader.IsRemote)
                {
                    var builder = new UriBuilder(context.Request.GetEncodedUrl())
                    {
                        Host = leader.Endpoint.Address.ToString(),
                        Port = applicationPortHint ?? context.Connection.LocalPort
                    };
                    redirection(context.Response, builder.Uri);
                }
                else
                    return next(context);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Defines that the relative path should be handled by a leader node only.
        /// </summary>
        /// <remarks>
        /// If the current node is not the leader then request will be
        /// redirected automatically to the leader node with 302 (Moved Permanently).
        /// If there are no consensus then the request will be failed with 503 (Service Unavailable).
        /// You can override redirection behavior using custom <paramref name="redirection"/>.
        /// <paramref name="applicationPortHint"/> used to highligh real port of the application endpoints in the cluster.
        /// This parameter can be used if your deployment is based on Docker. If it is not specified then router trying to add
        /// local port of the TCP listener. This may be invalid due to port mappings in Docker.
        /// </remarks>
        /// <param name="builder">The request processing pipeline builder.</param>
        /// <param name="path">The path that a leader must handle.</param>
        /// <param name="applicationPortHint">The port number to be inserted into Location header instead of automatically detected port of the local TCP listener.</param>
        /// <param name="redirection">The redirection logic.</param>
        /// <returns>The request processing pipeline builder.</returns>
        public static IApplicationBuilder RedirectToLeader(this IApplicationBuilder builder, PathString path, int? applicationPortHint = null, Action<HttpResponse, Uri> redirection = null)
        {
            var cluster = builder.ApplicationServices.GetRequiredService<RaftHttpCluster>();
            return builder.Map(path, pathBuilder => pathBuilder.Use(next => new RedirectionMiddleware(cluster, next, applicationPortHint, redirection).Redirect));
        }
    }
}