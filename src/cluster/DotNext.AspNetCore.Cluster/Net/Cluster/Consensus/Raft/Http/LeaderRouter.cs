using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using static Microsoft.Net.Http.Headers.HeaderNames;

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
            private readonly ICluster cluster;
            private readonly int? applicationPortHint;
            private readonly Func<HttpResponse, Uri, Task> redirection;
            private readonly PathString pathMatch;

            internal RedirectionMiddleware(ICluster cluster, RequestDelegate next, PathString pathMatch, int? applicationPortHint, Func<HttpResponse, Uri, Task>? redirection)
            {
                this.cluster = cluster;
                this.next = next;
                this.applicationPortHint = applicationPortHint;
                this.redirection = redirection ?? Redirect;
                this.pathMatch = pathMatch;
            }

            private static Task Redirect(HttpResponse response, Uri leaderUri)
            {
                response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                response.Headers[Location] = leaderUri.AbsoluteUri;
                return Task.CompletedTask;
            }

            private Task Redirect(HttpContext context, EndPoint leader)
            {
                string targetHost;
                switch (leader)
                {
                    case IPEndPoint ip:
                        targetHost = ip.Address.ToString();
                        break;
                    case DnsEndPoint dns:
                        targetHost = dns.Host;
                        break;
                    default:
                        // endpoint type is unknown so respond to the client without redirection
                        context.Response.StatusCode = StatusCodes.Status501NotImplemented;
                        return context.Response.WriteAsync(ExceptionMessages.UnsupportedRedirection);
                }

                return redirection(context.Response, new UriBuilder(context.Request.GetEncodedUrl()) { Host = targetHost, Port = applicationPortHint ?? context.Connection.LocalPort }.Uri);
            }

            internal Task Redirect(HttpContext context)
            {
                if (context.Request.Path.StartsWithSegments(pathMatch, StringComparison.OrdinalIgnoreCase))
                {
                    var leader = cluster.Leader;
                    if (leader is null)
                    {
                        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                        return Task.CompletedTask;
                    }

                    if (leader.IsRemote)
                        return Redirect(context, leader.EndPoint);
                }

                return next(context);
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
        /// <paramref name="applicationPortHint"/> used to highlight the real port of the application endpoints in the cluster.
        /// This parameter can be used if your deployment is based on Docker. If it is not specified then router trying to add
        /// local port of the TCP listener. This may be invalid due to port mappings in Docker.
        /// </remarks>
        /// <param name="builder">The request processing pipeline builder.</param>
        /// <param name="path">The path that a leader must handle.</param>
        /// <param name="applicationPortHint">The port number to be inserted into Location header instead of automatically detected port of the local TCP listener.</param>
        /// <param name="redirection">The redirection logic.</param>
        /// <returns>The request pipeline builder.</returns>
        public static IApplicationBuilder RedirectToLeader(this IApplicationBuilder builder, PathString path, int? applicationPortHint = null, Func<HttpResponse, Uri, Task>? redirection = null)
        {
            var cluster = builder.ApplicationServices.GetRequiredService<ICluster>();
            return builder.Use(next => new RedirectionMiddleware(cluster, next, path, applicationPortHint, redirection).Redirect);
        }
    }
}