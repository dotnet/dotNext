using System.ComponentModel;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Membership;
using Messaging;
using IFailureDetector = Diagnostics.IFailureDetector;

/// <summary>
/// Represents a host of local Raft cluster node.
/// </summary>
/// <remarks>
/// This class is useful when you want to host multiple Raft clusters inside of the same application.
/// For instance, if you want to implement sharding.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public class RaftClusterHttpHost : Disposable, IHostedService, IAsyncDisposable
{
    private readonly RaftHttpCluster cluster;

    /// <summary>
    /// Creates a new unstarted Raft cluster locally.
    /// </summary>
    /// <remarks>
    /// A list of required services that must be provided by <paramref name="activationContext"/>:
    /// <list type="table">
    /// <item>
    /// <term><see cref="IOptionsMonitor{T}"/> of type <see cref="HttpClusterMemberConfiguration"/></term>
    /// <description>Provides configuration of the local cluster member.</description>
    /// </item>
    /// <item>
    /// <term><see cref="ILoggerFactory"/></term>
    /// <description>Provides logging infrastructure.</description>
    /// </item>
    /// </list>
    /// A list of optional services that may be provided by <paramref name="activationContext"/>:
    /// <list type="table">
    /// <item>
    /// <term><see cref="IInputChannel"/></term>
    /// <description>Provides custom message processing.</description>
    /// </item>
    /// <item>
    /// <term><see cref="IClusterMemberLifetime"/></term>
    /// <description>Provides instance lifetime hooks.</description>
    /// </item>
    /// <item>
    /// <term><see cref="IClusterConfigurationStorage{TAddress}"/> of type <see cref="UriEndPoint"/></term>
    /// <description>Provides cluster configuration storage.</description>
    /// </item>
    /// <item>
    /// <term><see cref="IHttpMessageHandlerFactory"/></term>
    /// <description>
    /// Provides <see cref="HttpMessageHandler"/> implementation used by the local
    /// node to communicate with other cluster members.
    /// This is a key interface that can be used to share the same HTTP connection between
    /// multiple cluster instances when accessing the same node.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="MetricsCollector"/></term>
    /// <description>Allows to capture runtime metrics associated with the local node.</description>
    /// </item>
    /// <item>
    /// <term><see cref="ClusterMemberAnnouncer{TAddress}"/> of type <see cref="UriEndPoint"/></term>
    /// <description>Allows to announce a new node the cluster leader.</description>
    /// </item>
    /// <item>
    /// <term><see cref="Func{TimeSpan, IRaftClusterMember, IFailureDetector}"/> with generic arguments <see cref="TimeSpan"/>, <see cref="IRaftClusterMember"/>, and <see cref="IFailureDetector"/></term>
    /// <description>Provides failure detection mechanism that allows the leader to remove unresponsive members from the cluster.</description>
    /// </item>
    /// </list>
    /// You can supply these services from the implementation of <see cref="IServiceProvider"/> interface provided
    /// by Dependency Injection container or override some of the supplied services using <see cref="ServiceProviderFactory"/>.
    /// </remarks>
    /// <param name="activationContext">A provider of services required for initialization of <see cref="IRaftHttpCluster"/> implementation.</param>
    /// <param name="loggingCategory">The category of the logs to be produced by the implementation.</param>
    public RaftClusterHttpHost(IServiceProvider activationContext, string loggingCategory)
    {
        cluster = new(
            config: activationContext.GetRequiredService<IOptionsMonitor<HttpClusterMemberConfiguration>>(),
            messageHandlers: activationContext.GetServices<IInputChannel>(),
            logger: activationContext.GetRequiredService<ILoggerFactory>().CreateLogger(loggingCategory),
            configurator: activationContext.GetService<IClusterMemberLifetime>(),
            configStorage: activationContext.GetService<IClusterConfigurationStorage<UriEndPoint>>(),
            httpHandlerFactory: activationContext.GetService<IHttpMessageHandlerFactory>(),
#pragma warning disable CS0618
            metrics: activationContext.GetService<MetricsCollector>(),
#pragma warning restore CS0618
            announcer: activationContext.GetService<ClusterMemberAnnouncer<UriEndPoint>>())
        {
            FailureDetectorFactory = activationContext.GetService<Func<TimeSpan, IRaftClusterMember, IFailureDetector>>(),
        };
    }

    /// <summary>
    /// Gets relative URL path of Raft protocol handler.
    /// </summary>
    [CLSCompliant(false)]
    public PathString Path => cluster.ProtocolPath;

    /// <summary>
    /// Gets a local view of Raft cluster.
    /// </summary>
    public IRaftHttpCluster Cluster => cluster;

    /// <summary>
    /// Dispatches HTTP request to this instance.
    /// </summary>
    /// <remarks>
    /// This method is useful only when you want to host multiple Raft clusters inside of the same application.
    /// For instance, if you want to implement sharding.
    /// </remarks>
    /// <param name="context">HTTP request context.</param>
    /// <returns>The task representing asynchronous dispatch.</returns>
    [CLSCompliant(false)]
    public Task DispatchAsync(HttpContext context) => cluster.ProcessRequest(context);

    /// <summary>
    /// Starts hosting of local Raft node.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous operation result.</returns>
    public Task StartAsync(CancellationToken token) => cluster.StartAsync(token);

    /// <summary>
    /// Stops hosting of local Raft node.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous operation result.</returns>
    public Task StopAsync(CancellationToken token) => cluster.StopAsync(token);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            cluster.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override ValueTask DisposeAsyncCore() => cluster.DisposeAsync();

    /// <summary>
    /// Stops hosting of local Raft node and releases all managed resources.
    /// </summary>
    /// <returns>The task representing asynchronous operation result.</returns>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}