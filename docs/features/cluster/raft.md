Raft
====
Raft is a consensus algorithm suitable for building _master-replica_ clusters with the following features:
* Linearizability of operations
* Data consistency (weak or strong)
* Election of the leader node responsible for processing _write_ operations
* Replication
* Cluster configuration management

The core of Raft implementation is [RaftCluster&lt;TMember&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster`1) class which contains transport-agnostic implementation of Raft algorithm. First-class support of Raft in ASP.NET Core as well as other features are based on this class.

# Consensus
Correctness of consensus algorithm is tightly coupled with Write-Ahead Log defined via `AuditTrail` property of [IPersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.IPersistentState) interface or via Dependency Injection. If your application requires only consensus without replication of real data then [ConsensusOnlyState](xref:DotNext.Net.Cluster.Consensus.Raft.ConsensusOnlyState) implementation is used. Note that this implementation is used by default as well. It is lighweight and fast. However it doesn't store state on disk. Consider to use [persistent WAL](./wal.md) as fully-featured persistent log for Raft.

# State Recovery
The underlying state machine can be reconstruced at application startup using `InitializeAsync` method provided by implementation of [IPersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.IPersistentState) interface. Usually, this method is called by .NEXT infrastructure automatically.

[MemoryBasedStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) class exposes `ReplayAsync` method to do this manually. Read more about persistent Write-Ahead Log for Raft [here](./wal.md).

# Client Interaction
[Chapter 6](https://github.com/ongardie/dissertation/tree/master/clients) of Diego's dissertation contains recommendations about interaction between external client and cluster nodes. Raft implementation provided by .NEXT doesn't implement client session control as described in the paper. However, it offers all necessary tools for that:
1. `IPersistentState.EnsureConsistencyAsync` method waits until last committed entry is from leader's term
1. `IReplicationCluster.ForceReplicationAsync` method initiates a new round of heartbeats and waits for reply from the majority of nodes
1. `IRaftCluster.Lease` property to gets the lease that can be used for linearizable read
1. `IRaftCluster.ReplicateAsync` method to append, replicate and commit the log entry. Useful for implementing _write_ operations
1. `IRaftCluster.ApplyReadBarrierAsync` method to insert a barrier to achieve linearizable read
1. `IRaftCluster.LeadershipToken` property provides [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken) that represents a leadership state. If the local node is a leader then the token is in non-signaled state. If the local node is a follower node then the token is in canceled state. If local node is downgrading from the leader to the follower state then the token will be moved to the canceled state. This token is useful when implementing _write_ operations and allow to abort asynchronous operation in case of downgrade

Elimination of duplicate commands received from clients should be implemented manually because basic framework is not aware about underlying network transport.

## Linearizability
[Linearizability](https://en.wikipedia.org/wiki/Linearizability) requires the results of a read to reflect
a state of the system sometime after the read was initiated; each read must at least return the results
of the latest committed write. For instance, if the client performs _Write_ operation on variable _A_ and immediately requests variable _A_ back then _A_ must have the value which is equal to the value provided by _Write_ operation or more recent value. Without the linearizability, the client can see stale value of _A_. In other words, there is no guarantee that the client will able to see the result of its own _Write_ operation. A system that allowed stale reads would only provide serializability, which is a weaker form of consistency.

Linearizable read can be achieved in Raft naturally. _Read_ operation can be performed on leader or follower nodes.

`IRaftCluster.Lease` property exposes leadership lease than quarantees that the leader cannot be changed during that lease. This method of provoding linearizability doesn't require extra round of heartbeats. As a result, this is the most performant way to process read-only queries. However, the duration of the lease depends on _clockDriftBound_. Here's the citation from Raft paper:
> The lease approach assumes a bound on clock drift across servers (over a given time period, no server’s clock increases more than this bound times any other). Discovering and maintaining this bound might present operational challenges (e.g., due to scheduling and garbage collection pauses, virtual machine migrations, or clock rate adjustments for time synchronization). If the assumptions are violated, the system could return arbitrarily stale information.

Lease approach can be used only if processing of all read-only queries performing by the leader node.

Another approach is to use _read barrier_. The barrier is provided by `IRaftCluster.ApplyReadBarrierAsync` method. It allows to process read-only queries by follower nodes. In case of follower node, the method instructs leader node to execute a new round of heartbeats (with help of `ForceReplicationAsync` method). The follower waits for its state machine to advance at least as far as the index of the last committed log entry on the leader node. These actions are enough to satisfy linearizability. As you can see, this approach leads to extra overhead caused by network communication.

Lease and read barrier are mechanisms for linearizable reads provided out-of-the-box. However, it's possible to use any other approach. For instance, the server respond with the commit index for each _Write_ request. The client can update and remember this value locally and provide it with read-only query. When _Read_ request is received, the server may call `IPersistentState.WaitForCommitAsync` to ensure that the log contains the index of the last committed log entry by the client.

# Node Bootstrapping
The node can be started in two modes:
* **Cold Start** means that the starting node is the initial node in the cluster. In that case, the node adds itself to the cluster configuration in committed state.
* **Announcement** means that the starting node must be announced through the leader, added to the cluster configuration and committed by the majority of nodes. In that case the node is started in _Standby_ mode and waits until it will be added to the configuration by leader node and replicated to the that node

The node is started using `StartAsync` method of [RaftCluster&lt;TMember&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster`1) class doesn't mean that the node is ready to serve client requests. To ensure that the node is bootstrapped correctly, use _Readiness Probe_. The probe is provided through `Readiness` property of [IRaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftCluster) interface.

# Cluster Configuration Management
Raft supports cluster configuration management out-of-the-box. Cluster configuration is a set of cluster members consistently stored on the nodes. The leader node is responsible for processing amendments of the configuration and replicating the modified configuration to follower nodes. Thus, a list of cluster members is always in consistent state.

The configuration can be in two states:
* _Active_ configuration which is used by the leader node for sending heartbeats. This type of configuration is always acknowledged by the majority of nodes and, as a result, the same on every node in the cluster
* _Proposed_ configuration which is created by leader node as a response to configuration change. This type of configuration must be replicated and confirmed by the majority of nodes to be transformed into _Active_ configuration.

Proposed configuration is similar to uncommitted log entries in Raft log. Due to simplicity, the proposed configuration can be created using the following operations:
* Add a new member
* Remove the existing member

It's not possible to remove or add multiple members at a time. Instead, you need to add or remove single member and replicate that change. When the proposed configuration is accepted by the majority of nodes, the leader node turns that configuration into the active configuration.

[IClusterConfigurationStorage](xref:DotNext.Net.Cluster.Consensus.Raft.Membership.IClusterConfigurationStorage) interface is responsible for maintaining cluster configuration. There are two possible storages:
* _In-memory_ storage that stores configuration in the memory. Restarting the node leads to configuration loss
* _Persistent_ storage that stores configuration in the file system

When a new node is added, it passes through warmup procedure. The leader node attempts to replicate as much as possible log entries to the added node. The number of rounds for catch up can be configured by `WarmupRounds` configuration property. When the leader node decided that the new node is in sync then it adds the address of that node to the proposed configuration. When the proposed configuration becomes the active configuration, readiness probe of the added node turning into the signaled state.

# Network Transport
.NEXT supports the following network transports:
* HTTP 1.1, HTTP 2.0 and HTTP 3.0
* TCP transport
* UDP transport
* Generic transport on top of [ASP.NET Core Connections](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.connections) abstractions. See [CustomTransportConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster.CustomTransportConfiguration) class for more information.

TCP and UDP network transports shipped with `DotNext.Net.Cluster` library without heavyweight dependencies such as ASP.NET Core or DotNetty. The library provides specialized [application protocol](https://en.wikipedia.org/wiki/Application_layer) on top of these transports which is binary protocol, highly optimized for Raft purposes and provide maximum bandwidth in contrast to HTTP. However, additional features for cluster programming are limited:
* General-purpose messaging between nodes is not supported via [IMessageBus](xref:DotNext.Net.Cluster.Messaging.IMessageBus) interface

Cluster programming model using TCP, UDP, and generic transports is unified and exposed via [RaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster) class. The following example demonstrates usage of this class:
```csharp
using DotNext.Net.Cluster.Consensus.Raft;

RaftCluster.NodeConfiguration config = ...;//configuration of the local node
//configuring members in the cluster
config.Members.Add(new IPEndPoint(IPAddress.Loopback), 3262);
config.Members.Add(new IPEndPoint(IPAddress.Loopback), 3263);

using var cluster = new RaftCluster(config);
await cluster.StartAsync(CancellationToken.None); //starts hosting of the local node
//the code for working with cluster instance
await cluster.StopAsync(CancellationToken.None);    //stops hosting of the local node
```
The configuration of the local node depends on chosen network transport. [NodeConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster.NodeConfiguration) abstract class exposes common properties for both transports:

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| Metrics | No | **null** | Allows to specify custom metrics collector |
| PublicEndPoint | No | The same as `HostEndPoint` | Allows to specify real IP address of the host where cluster node launched. Usually it is needed when node executed inside of Docker container. If this parameter is not specified then cluster node may fail to detect itself because network interfaces inside of Docker container have different addresses in comparison with real host network interfaces |
| Partitioning | No | false | `true` if partitioning supported. In this case, each cluster partition may have its own leader, i.e. it is possible to have more that one leader for external observer. However, single partition cannot have more than 1 leader. `false` if partitioning is not supported and only one partition with majority of nodes can have leader. Note that cluster may be totally unavailable even if there are operating members presented |
| HeartbeatThreshold | No | 0.5 | Specifies frequency of heartbeat messages generated by leader node to inform follower nodes about its leadership. The range is (0, 1). The lower the value means that the messages are generated more frequently and vice versa |
| LowerElectionTimeout, UpperElectionTimeout | No | 150 | Defines range for election timeout (in milliseconds) which is picked randomly inside of it for each cluster member. If cluster node doesn't receive heartbeat from leader node during this timeout then it becomes a candidate and start a election. The recommended value for  _upperElectionTimeout_ is `2  X lowerElectionTimeout` |
| MemoryAllocator | No | Memory pool from _PipeConfig_ property | Memory allocator used to allocate memory for network packets |
| Metadata | No | Empty dictionary | A set of metadata properties associated with the local node |
| RequestTimeout | No | _UpperElectionTimeout_ | Defines request timeout for accessing cluster members across the network |
| LoggerFactory | No | [NullLoggerFactory.Instance](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.abstractions.nullloggerfactory.instance) | The logger factory |
| Standby | No | **false** | **true** to prevent election of the cluster member as a leader. It's useful to configure the nodes available for read-only operations only |
| ConfigurationStorage | Yes | N/A | Represents a storage for the list of cluster members. You can use `UseInMemoryConfigurationStorage` method for testing purposes |
| Announcer | No | **null** | A delegate of type [ClusterMemberAnnouncer&lt;TAddress&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.Membership.ClusterMemberAnnouncer`1) that can be used to announce a new node on leader |
| WarmupRounds | No | 10 | The numbers of rounds used to warmup a fresh node which wants to join the cluster |
| ColdStart | No | **true** | **true** to start the initial node in the cluster. In case of cold start, the node doesn't announce itself. **false** to start the node in standby node and wait for announcement |

By default, all transport bindings for Raft use in-memory configuration storage.

Cluster configuration management is represented by the following methods declared in [RaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster) class:
* `AddMemberAsync` to add and catch up a new node
* `RemoveMemberAsync` to remove the existing node

`AddMemberAsync` can be called by deployment script, manually by the administrator or through _Announcer_. `RemoveMemberAsync` can be called as a part of graceful shutdown (planned deprovisioning) or manually by the administrator.

## HTTP transport and ASP.NET Core
`DotNext.AspNetCore.Cluster` library is an extension for ASP.NET Core for writing microservices and supporting the following features:
1. Messaging is fully supported and organized through HTTP 1.1, HTTP 2.0 or HTTP 3.0 protocol including TLS
1. Replication is fully supported
1. Consensus is fully supported and based on Raft algorithm
1. Tight integration with ASP.NET Core ecosystem such as Dependency Injection and Configuration Object Model
1. Compatible with Kestrel or any other third-party web host
1. Detection of changes in the list of cluster nodes via configuration

These extensions are located in `DotNext.Net.Cluster.Consensus.Raft.Http` namespace.

This implementation is WAN friendly because it uses reliable network transport and supports TLS. It is good choice if your cluster nodes communicate over Internet or any other unreliable network. However, HTTP leads to performance and traffic overhead. Moreover, the library depends on ASP.NET Core.

Web application is treated as cluster node. The following example demonstrates how to turn ASP.NET Core application into cluster node:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler();	//informs that processing pipeline should handle Raft-specific requests
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.UsePersistentConfigurationStorage("/path/to/folder");
    }
}

IHost host = new HostBuilder()
    .ConfigureWebHost(webHost => webHost
        .UseKestrel(options => options.ListenLocalhost(80))
        .UseStartup<Startup>()
    )
    .JoinCluster()  //registers all necessary services required for normal cluster node operation
    .Build();
```

Note that `JoinCluster` method should be called after `ConfigureWebHost`. Otherwise, the behavior of this method is undefined.

`JoinCluster` method has overloads that allow to specify custom configuration section containing the configuration of the local node.

`UseConsensusProtocolHandler` method should be called before registration of any authentication/authorization middleware.

`UsePersistentConfigurationStorage` allows to configure a persistent storage for the cluster configuration. Additionally, you can use `UseInMemoryConfigurationStorage` method and keep the configuration in the memory. However, it's not recommended for production use.

### Dependency Injection
The application may request the following services from ASP.NET Core DI container:
* [ICluster](xref:DotNext.Net.Cluster.ICluster)
* [IRaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftCluster) represents Raft-specific version of `ICluster` interface
* [IMessageBus](xref:DotNext.Net.Cluster.Messaging.IMessageBus) for point-to-point messaging between nodes
* [IPeerMesh&lt;IRaftClusterMember&gt;](xref:DotNext.Net.IPeerMesh`1) for tracking changes in cluster membership
* [IReplicationCluster&lt;IRaftLogEntry&gt;](xref:DotNext.Net.Cluster.Replication.IReplicationCluster`1) to work with audit trail used for replication. [IRaftLogEntry](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry) is Raft-specific representation of the record in the audit trail
* [IReplicationCluster](xref:DotNext.Net.Cluster.Replication.IReplicationCluster) to work with audit trail in simplified manner
* [IRaftHttpCluster](xref:DotNext.Net.Cluster.Consensus.Raft.Http.IRaftHttpCluster) provides HTTP-specific extensions to [IRaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftCluster) interface plus cluster management methods

### Configuration
The application should be configured properly to work as a cluster node. The following JSON represents the example of configuration:
```json
{
	"partitioning" : false,
	"lowerElectionTimeout" : 150,
	"upperElectionTimeout" : 300,
	"metadata" :
	{
		"key": "value"
	},
	"requestJournal" :
	{
		"memoryLimit": 5,
		"expiration": "00:00:10",
		"pollingInterval" : "00:01:00"
	},
    "clientHandlerName" : "raftClient",
	"port" : 3262,
	"heartbeatThreshold" : 0.5,
    "requestTimeout" : "00:01:00",
    "rpcTimeout" : "00:00:150",
    "keepAliveTimeout": "00:02:00",
    "openConnectionForEachRequest" : false,
    "clockDriftBound" : 1.0,
    "coldStart" : true,
    "standby" : false,
    "warmupRounds" : 10,
    "protocolVersion" : "auto",
    "protocolVersionPolicy" : "RequestVersionOrLower",
}
```

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| partitioning | No | false | `true` if partitioning supported. In this case, each cluster partition may have its own leader, i.e. it is possible to have more that one leader for external observer. However, single partition cannot have more than 1 leader. `false` if partitioning is not supported and only one partition with majority of nodes can have leader. Note that cluster may be totally unavailable even if there are operating members presented
| lowerElectionTimeout, upperElectionTimeout | No | 150, 300 |  Defines range for election timeout (in milliseconds) which is picked randomly inside of it for each cluster member. If cluster node doesn't receive heartbeat from leader node during this timeout then it becomes a candidate and start a election. The recommended value for  _upperElectionTimeout_ is `2  X lowerElectionTimeout`
| metadata | No | empty dictionary | A set of key/value pairs to be associated with cluster node. The metadata is queriable through `IClusterMember` interface |
| openConnectionForEachRequest | No | false | `true` to create TCP connection every time for each outbound request. `false` to use HTTP KeepAlive |
| clientHandlerName | No | raftClient | The name to be passed into [IHttpMessageHandlerFactory](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.ihttpmessagehandlerfactory) to create [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker) used by Raft client code |
| requestJournal:memoryLimit | No | 10 | The maximum amount of memory (in MB) utilized by internal buffer used to track duplicate messages |
| requestJournal:expiration | No | 00:00:10 | The eviction time of the record containing unique request identifier |
| requestJournal:pollingInterval | No | 00:01:00 | Gets the maximum time after which the buffer updates its memory statistics |
| heartbeatThreshold | No | 0.5 | Specifies frequency of heartbeat messages generated by leader node to inform follower nodes about its leadership. The range is (0, 1). The lower the value means that the messages are generated more frequently and vice versa. |
| protocolVersion | No | auto | HTTP protocol version to be used for the communication between members. Possible values are `auto`, `http1`, `http2`, `http3` |
| protocolVersionPolicy | No | RequestVersionOrLower | Specifies behaviors for selecting and negotiating the HTTP version for a request. Possible values are `RequestVersionExact`, `RequestVersionOrHigher`, `RequestVersionOrLower` |
| requestTimeout | No | `upperElectionTimeout` | Request timeout used to access cluster members across the network using HTTP client |
| rpcTimeout | No | `upperElectionTimeout` / 2 | Request timeout used to send Raft-specific messages to cluster members. Must be less than or equal to _requestTimeout_ parameter |
| standby | No | false | **true** to prevent election of the cluster member as a leader. It's useful to configure the nodes available for read-only operations only |
| coldStart | No | true | **true** to start the initial node in the cluster. In case of cold start, the node doesn't announce itself. **false** to start the node in standby node and wait for announcement |
| clockDriftBound | No | 1.0 | A bound on clock drift across servers. This value is used to calculate the leader lease duration. The lease can be obtained via `IRaftCluster.Lease` property. The lease approach assumes a bound on clock drift across servers: over a given time period, no server’s clock increases more than this bound times any other |
| warmupRounds | No | 10 | The numbers of rounds used to warmup a fresh node which wants to join the cluster |

`requestJournal` configuration section is rarely used and useful for high-load scenarios only.

Choose `lowerElectionTimeout` and `upperElectionTimeout` according with the quality of your network. If these values are small then you'll get a frequent leader re-elections.

### Controlling node lifetime
The service implementing `IRaftCluster` is registered as singleton service. It starts receiving Raft-specific messages immediately. Therefore, you can loose some events raised by the service such as `LeaderChanged` at starting point. To avoid that, you can implement [IClusterMemberLifetime](xref:DotNext.Net.Cluster.Consensus.Raft.IClusterMemberLifetime) interface and register implementation as a singleton.

```csharp
using DotNext.Net.Cluster.Consensus.Raft;
using System.Collections.Generic;

internal sealed class MemberLifetime : IClusterMemberLifetime
{
	private static void LeaderChanged(ICluster cluster, IClusterMember leader) {}

	void IClusterMemberLifetime.OnStart(IRaftCluster cluster, IDictionary<string, string> metadata)
	{
		metadata["key"] = "value";
		cluster.LeaderChanged += LeaderChanged;
	}

	void IClusterMemberLifetime.OnStop(IRaftCluster cluster)
	{
		cluster.LeaderChanged -= LeaderChanged;
	}
}
```

Additionally, the hook can be used to modify metadata of the local cluster member.

### HTTP Client Behavior
HTTP binding for Raft uses [HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) for communication between cluster nodes. The client itself delegates all operations to [HttpMessageHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler). It's not recommended to use [HttpClientHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler) because it has inconsistent behavior on different platforms. For instance, on Linux it invokes _libcurl_. Raft implementation uses `Timeout` property of `HttpClient` to establish request timeout. It's always defined as `upperElectionTimeout` by .NEXT infrastructure. To demonstrate the inconsistent behavior let's introduce three cluster nodes: _A_, _B_ and _C_. _A_ and _B_ have been started except _C_:
* On Windows the leader will not be elected even though the majority is present - 2 of 3 nodes are available. This is happening because Connection Timeout is equal to Response Timeout, which is equal to `upperElectionTimeout`.
* On Linux everything is fine because Connection Timeout less than Response Timeout

By default, Raft implementation uses [SocketsHttpHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler). However, the handler can be overridden using [IHttpMessageHandlerFactory](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.ihttpmessagehandlerfactory). You can implement this interface manually and register that implementation as a singleton. .NEXT tries to use this interface if it is registered as a factory of custom [HttpMessageHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler). The following example demonstrates how to implement this interface and create platform-independent version of message invoker:

```csharp
using System;
using System.Net.Http;

internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
{
	public HttpMessageHandler CreateHandler(string name) => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
}
```

In practice, `ConnectTimeout` should be equal to or less than `lowerElectionTimeout` configuration property. Note that `name` parameter is equal to the `clientHandlerName` configuration property when handler creation is requested by Raft implementation.

### Redirection to Leader
Client interaction requires automatic detection of a leader node. Cluster Development Suite provides a way to automatically redirect requests to the leader node if it was originally received by a follower node. The redirection is organized with help of _307 Temporary Redirect_ status code. Every follower node knows the actual address of the leader node. If cluster or its partition doesn't have leader then node returns _503 Service Unavailable_. 

Automatic redirection can be configured using [RedirectToLeader](xref:DotNext.Net.Cluster.Consensus.Raft.Http.ConfigurationExtensions) extension method.

```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class Startup
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration) => this.configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler()
			.RedirectToLeader("/endpoint1")
			.RedirectToLeader("/endpoint2");
    }

    public void ConfigureServices(IServiceCollection services)
    {
    }
}
```

This redirection can be transparent to actual client if you use reverse proxy server such as NGINX. Reverse proxy can automatically handle the redirection without returning control to the client.

It is possible to change default behavior of redirection where _307 Temporary Redirect_ status code is used. You can pass custom implementation into the optional parameter of `RedirectToLeader` method.

The following example demonstrates how to return _404 Not Found_ and location of Leader node as its body.

```csharp
private static Task CustomRedirection(HttpResponse response, Uri leaderUri)
{
    response.StatusCode = StatusCodes.Status404NotFound;
    return response.WriteAsync(leaderUri.AbsoluteUri);
}

public void Configure(IApplicationBuilder app)
{
    app.UseConsensusProtocolHandler()
        .RedirectToLeader("/endpoint1", redirection: CustomRedirection);
}
```

The customized redirection should be as fast as possible and don't block the caller.

### Port mapping
Redirection mechanism trying to construct valid URI of the leader node based on its actual IP address. Identification of the address is not a problem unlike port number. The infrastructure cannot use the port if its [WebHost](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webhost) because of Hosted Mode or the port from the incoming `Host` header because it can be rewritten by reverse proxy. The only way is to use the inbound port of the TCP listener responsible for handling all incoming HTTP requests. It is valid for the non-containerized environment. Inside of the container the ASP.NET Core application port is mapped to the externally visible port which not always the same. In this case you can specify port for redirections explicitly as follows:

```csharp
public void Configure(IApplicationBuilder app)
{
    app.UseConsensusProtocolHandler()
      .RedirectToLeader("/endpoint1", applicationPortHint: 3265);
}
```

### Messaging
Cluster Programming Suite supports messaging beween nodes through HTTP out-of-the-box. However, the infrastructure don't know how to handle custom messages. Therefore, if you want to utilize this functionality then you need to implement [IInputChannel](xref:DotNext.Net.Cluster.Messaging.IInputChannel) interface.

Messaging inside of cluster supports redirection to the leader as well as for external client. But this mechanism implemented differently and exposed as `IInputChannel` interface via `LeaderRouter` property of [IMessageBus](xref:DotNext.Net.Cluster.Messaging.IMessageBus) interface.

### Replication
Raft algorithm requires additional persistent state in order to basic audit trail. This state is represented by [IPersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.IPersistentState) interface. By default, it is implemented as [ConsensusOnlyState](xref:DotNext.Net.Cluster.Consensus.Raft.ConsensusOnlyState) which is suitable only for applications that doesn't have replicated state. If your application has it, then use [MemoryBasedStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) class or implement the interface from scratch. The implementation can be injected explicitly via `AuditTrail` property of [IRaftCluster](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftCluster) interface or implicitly via Dependency Injection. The explicit registration should be done inside of the user-defined implementation of [IClusterMemberLifetime](xref:DotNext.Net.Cluster.Consensus.Raft.IClusterMemberLifetime) interface registered as a singleton service in ASP.NET Core application. The implicit injection requires registration of a singleton service implementing [IPersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.IPersistentState) interface. `UsePersistenceEngine` extension method of [RaftClusterConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftClusterConfiguration) class can be used for that purpose.

Information about reliable persistent state that uses non-volatile storage is located in the separated [article](./wal.md). However, its usage turns your microservice into stateful service because its state must be persisted on a disk. Consider this fact if you are using containerization technologies such as Docker or LXC.

### Metrics
It is possible to measure runtime metrics of Raft node internals using [HttpMetricsCollector](xref:DotNext.Net.Cluster.Consensus.Raft.Http.HttpMetricsCollector) class. The reporting mechanism is agnostic to the underlying metrics delivery library such as [AppMetrics](https://github.com/AppMetrics/AppMetrics).

The class contains methods that are called automatically. You can override them and implement necessary reporting logic. By default, these methods report metrics to .NET Performance Counters.

The metrics collector should be registered as singleton service using Dependency Injection. However, the type of the service used for registration should of [MetricsCollector](xref:DotNext.Net.Cluster.Consensus.Raft.MetricsCollector) type.

```csharp
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class MyCollector : HttpMetricsCollector
{
	public override void ReportResponseTime(TimeSpan value)
    {
		//report response time of the cluster member
    } 

	public override void ReportBroadcastTime(TimeSpan value)
    {
		//report broadcast time measured during sending the request to all cluster members
    }
}

sealed class Startup 
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration) => this.configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler()
			.RedirectToLeader("/endpoint1")
			.RedirectToLeader("/endpoint2");
    }

    public void ConfigureServices(IServiceCollection services)
    {
		services.AddSingleton<MetricsCollector, MyCollector>();
    }
}
```

It is possible to derive directly from [MetricsCollector](xref:DotNext.Net.Cluster.Consensus.Raft.MetricsCollector) if you don't need to receive metrics related to HTTP-specific implementation of Raft algorithm.

## TCP Transport
TCP transport used as bottom layer for specialized application protocol aimed to efficient transmission of Raft messages. This transport can be configured using [TcpConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster.TcpConfiguration) class:
```csharp
using DotNext.Net.Cluster.Consensus.Raft;

RaftCluster.NodeConfiguration config = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback));
using var cluster = new RaftCluster(config);
await cluster.StartAsync(CancellationToken.None); //starts hosting of the local node
//the code for working with cluster instance
await cluster.StopAsync(CancellationToken.None);    //stops hosting of the local node
```

Constructor expecting address and port used for hosting of the local node. 

The following table describes configuration properties applicable to TCP transport:

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| ServerBacklog | No | Equal to the number of cluster members | The number of active incoming connections allowed by the local node |
| LingerOption | No | Not enabled | The configuration that specifies whether a TCP socket will delay its closing in an attempt to send all pending data |
| GracefulShutdownTimeout | No | _LowerElectionTimeout_ | The timeout of graceful shutdown of active incoming connections |
| TransmissionBlockSize | No | 65535 | The size, in bytes, of internal memory block used for sending packets. If your network has high packet loss then you can decrease this value to avoid retransmission of large blocks. |
| RequestTimeout | No | _LowerElectionTimeout / 2_ | A timeout used for Raft RPC call. Must be less than or equal to _LowerElectionTimeout_ |
| ConnectTimeout | No | _LowerElectionTimeout / 2_ | TCP connection timeout. Must be less than or equal to _RequestTimeout_ |
| SslOptions | No | _N/A_ | Allows to enable and configure transport-level encryption using SSL and X.509 certificates |
| TimeToLive | No | 64 |  Time To Live (TTL) value of Internet Protocol (IP) packets |

TCP transport is WAN friendly and support transport-level encryption. However, the underlying application-level protocol is binary and can be a problem for corporate firewalls.

The recommended relationship between timeouts: `ConnectTimeout < RequestTimeout < LowerElectionTimeout`. In practice, _ConnectTimeout_ should be as small as possible to avoid impact on the cluster by disconnected nodes.

## UDP Transport
UDP transport used as bottom layer for specialized application protocol aimed to efficient transmission of Raft messages. This transport doesn't use persistent connection in contrast to TCP. As a result, it has no TCP overhead related to congestion and flow control of messages. These capabilities are implemented by application protocol itself. However, retransmission of lost packets is not implemented. The transport uses pessimistic approach and interprets lost packets as connection timeout. This is reasonable approach because the leader node examines other cluster members periodically and the next attempt may be successful. Some Raft messages such as _Vote_ and _Heartbeat_ with empty set of log entries (or if log entries are small enough) for replication can be easily placed to single datagram without fragmentation.

The transport has very low overhead which is equal to ~20 bytes per datagram. Therefore, most Raft messages can be placed to single datagram without streaming per request.

UDP transport cannot detect path [MTU](https://en.wikipedia.org/wiki/Maximum_transmission_unit) automatically and, by default, it uses minimal safe size of the datagram to avoid fragmentation. If need automatic path MTU discovery then use [MTU discovery](../core/mtu.md) mechanism from .NEXT. After that, you can specify datagram size using configuration properties.

This transport can be configured using [UdpConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster.UdpConfiguration) class:

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| ServerBacklog | No | Equal to the number of cluster members | The number of incoming requests that can be handled simultaneously |
| ClientBacklog | No | Equal to the number of logical processor on the host machine | The number of outbound requests that can be initiated by the local node |
| DontFragment | No | true | Indicates that datagram cannot be fragmented by the underlying network layer such as IP (DF flag) |
| DatagramSize | No | 300 bytes | Represents UDP datagram size. For maximum performance, this property must be set to the maximum allowed transmission unit size by your network |
| LocalEndPoint | No | 0.0.0.0 with random port | Used for receiving responses from other cluster nodes |
| PipeConfig | No | [PipeOptions.Default](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipeoptions.default) | The configuration of I/O pipeline used for passing bytes between application and network transport |
| TimeToLive | No | 64 |  Time To Live (TTL) value of Internet Protocol (IP) packets |

The following example demonstrates configuration of UDP transport:
```csharp
using DotNext.Net.Cluster.Consensus.Raft;

RaftCluster.NodeConfiguration config = new RaftCluster.UdpConfiguration(new IPEndPoint(IPAddress.Loopback));
using var cluster = new RaftCluster(config);
await cluster.StartAsync(CancellationToken.None); //starts hosting of the local node
//the code for working with cluster instance
await cluster.StopAsync(CancellationToken.None);    //stops hosting of the local node
```

If you are using Docker/LXC/Windows container for the clustered microservices based on UDP transport then you can leave `LocalEndPoint` untouched. Otherwise, it's recommended to use the address of the appropriate local network interface.

UDP transport is WAN unfriendly. It should not be used in unreliable networks. However, it's much faster than TCP transport. It is recommended to use this protocol in the following situations:
* Cluster nodes are hosted in the same rack
* Cluster nodes are hosted in the different racks but located in the same datacenter and racks connected by high-speed physical interface such as FibreChannel.
* Cluster nodes are in Docker/LXC/Windows containers running on the same physical host

# Example
There is Raft playground represented by RaftNode application. You can find this app [here](https://github.com/dotnet/dotNext/tree/master/src/examples/RaftNode). This playground allows to test Raft consensus protocol in real world using one of the supported transports: `http`, `tcp`, `tcp+ssl`, `udp`.

Each instance of launched application represents cluster node. All nodes can be started using the following script:
```bash
cd <dotnext>/src/examples/RaftNode
dotnet run -- http 3262
dotnet run -- http 3263
dotnet run -- http 3264
```

Every instance should be launched in separated Terminal session. After that, you will see diagnostics messages in `stdout` about election process. Press _Ctrl+C_ in the window related to the leader node and ensure that new leader will be elected.

Optionally, you can test replication powered by persistent WAL. To do that, you need to specify the name of folder which is used to store Write Ahead Log files
```bash
cd <dotnext>/src/examples/RaftNode
dotnet run -- http 3262 node1
dotnet run -- http 3263 node2
dotnet run -- http 3264 node3
```
Now you can see replication messages in each Terminal window. The replicated state stored in the `node1`, `node2` and `node3` folders. You can restart one of the nodes and make sure that its state is recovered correctly.

# Extensions
Raft implementation provided by .NEXT library contains some extensions of the original algorithm. For instance, _Standby_ extra state is added in addition to _Follower_, _Candidate_, and _Leader_ states. All those extensions are grouped into interfaces that can be found in [separated namespace](xref:DotNext.Net.Cluster.Consensus.Raft.Extensions).

## Automatic failure detection
Automatic Failure Detection is another extension to original Raft algorithm that allows cluster leader to detect unresponsive followers and remove those from the cluster configuration. This behavior allows to detect and tolerate permanent failures of a particular cluster node and remove it from the majority calculation to remain cluster available for writes. For instance, we have 7 nodes in the cluster. The cluster remains available if at least 4 nodes are alive. With failure detector, we can remove 3 faulty nodes and reconfigure the cluster dynamically to indicate that the cluster has 4 nodes only. In that case, the cluster remains available even with 3 nodes.

However, the current implementation needs to inform the rest of the cluster about faulty node. In other words, the cluster must be available for writes. If 4 of 7 nodes are detected as faulty in the same time, it is not possible to reconfigure the cluster because there is no majority to keep the leader working as expected.

[IFailureDetector](xref:DotNext.Diagnostics.IFailureDetector) interface is an extension point that provides failure detection algorithm. The library ships [φ Accrual Failure Detector](xref:DotNext.Diagnostics.PhiAccrualFailureDetector) as an efficient implementation of the detector which is based on anomalies of response time. By default, automatic failure detection is disabled. But the caller code can specify a factory for failure detectors. In that case, the internals of Raft implementation instantiate failure detector for each cluster member automatically on a leader's side. See [RaftCluster&lt;TMember&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster`1.FailureDetectorFactory) property for more information. In DI environment (ASP.NET Core), the factory can be registered as singleton service.

# Development and Debugging
It may be hard to reproduce the real cluster on developer's machine. You may want to run your node in _Debug_ mode and ensure that the node you're running is a leader node. To do that, you need to start the node in _Cold Start_ mode.

# Performance
The wire format is highly optimized for transferring log entries during the replication process over the wire. The most performance optimizations should be performed when configuring persistent Write-Ahead Log.

[MemoryBasedStateMachine]((xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine)) supports several log compaction modes. Some of them allow compaction in parallel with appending of new log entries. Read [this article](./wal.md) for more information about the available modes. _Background_ compaction provides precise control over the compaction. There are few ways to control it:
1. If you're using `UsePersistenceEngine` extension method for registering your engine based on `MemoryBasedStateMachine` then .NEXT infrastructure automatically detects the configured compaction mode. If it is _Background_ then it will register _compaction worker_ as a background service in ASP.NET Core. This worker provides _incremental background compaction_. You can override this behavior by implementing [ILogCompactionSupport](xref:DotNext.IO.Log.ILogCompactionSupport) in your persistence engine.
1. If you're registering persistence engine in DI container manually, you need to implement background compaction worker manually using [BackgroundService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.backgroundservice.startasync) class and call `ForceCompactionAsync` method in the overridden `ExecuteAsync` method.

_Incremental background compaction_ is the default strategy when _Background_ compaction enabled. The worker just waits for the commit and checks whether `MemoryBasedStateMachine.CompactionCount` property is greater than zero. If so, it calls `ForceCompactionAsync` with the compaction factor which is equal to 1. It provides minimal compaction of the log. As a result, the contention between the compaction worker and readers is minimal or close to zero.

# Guide: How To Implement Database
This section contains recommendations about implementation of your own database or distributed service based on .NEXT Cluster programming model. It can be K/V database, distributed UUID generator, distributed lock or anything else.

For memory-based state machine:
1. Derive from [MemoryBasedStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) class to implement core logic related to manipulation with state machine
    1. Override `ApplyAsync` method which contains interpretation of commands contained in log entries
    1. Override `CreateSnapshotBuilder` method which is responsible for log compaction
    1. Expose high-level data operations declared in the derived class in the form of interface. Let's assume that its name is `IDataEngine`
1. Declare class that is responsible for communication with leader node using custom messages
    1. This class aggregates reference to `IDataEngine`
    1. This class encapsulates logic for messaging with leader node
    1. This class acting as controller for API exposed to external clients
    1. Use `IRaftCluster.ApplyReadBarrierAsync` to ensure that the node is fully synchronized with the leader node
    1. Use `IRaftCluster.ReplicateAsync` for write operations
1. Expose data manipulation methods from class described above to clients using selected network transport
1. Implement duplicates elimination logic for write requests from clients
1. Call `ReplayAsync` method which is inherited from `MemoryBasedStateMachine` class at application startup. This step is not need if you're using Raft implementation for ASP.NET Core.

`ForceReplicationAsync` method doesn't provide strong guarantees that the log entry at the specified index will be replicated and committed on return. A typical code for processing a new log entry from the client might be look like this:
```csharp
IRaftCluster cluster = ...;
var term = cluster.Term;
await cluster.ReplicateAsync(new MyLogEntry(term), Timeout.InfiniteTimeSpan, token);
```

The same pattern is applicable to [disk-based state machine](xref:DotNext.Net.Cluster.Consensus.Raft.DiskBasedStateMachine) except snapshotting.

Designing binary format for custom log entries and interpreter for them may be hard. Examine [this](./wal.md) article to learn how to use Interpreter Framework shipped with the library.

# Guide: Custom Transport
Transport- and serialization-agnostic implementation of Raft is represented by [RaftCluster&lt;TMember&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster`1) class. It contains core consensus and replication logic but it's not aware about network-specific details. You can use this class as foundation for your own Raft implementation for particular network protocol. All you need is to implementation protocol-specific communication logic.  This chapter will guide you through all necessary steps.

> [!NOTE]
> The easiest way to support new network protocol (e.g. Bluetooth) is to use [CustomTransportConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.RaftCluster.CustomTransportConfiguration) class. However, it doesn't provide control over the serialization format of Raft messages. If you're looking for a way to provide custom application-level protocol for Raft, follow this guide.

## Existing Implementations
.NEXT library ships multiple network transports: 
* [RaftHttpCluster](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.cs) as a part of `DotNext.AspNetCore.Cluster` library offers HTTP 1.1/HTTP 2/HTTP 3 implementations adopted for ASP.NET Core framework
* [TransportServices](https://github.com/dotnet/dotNext/tree/develop/src/cluster/DotNext.Net.Cluster/Net/Cluster/Consensus/Raft/TransportServices) as a part of `DotNext.Net.Cluster` library contains reusable network transport layer for UDP and TCP transport shipped as a part of this library

All these implementations can be used as examples of transport for Raft messages.

## Architecture
`RaftCluster` contains implementation of consensus and replication logic so your focus is network-specific programming. First of all, you need to derive from this class. There are two main extensibility points when network-specific programing needed:
* `TMember` generic parameter which should be replaced with actual type argument by the derived class. Actual type argument should be a class implementing [IRaftClusterMember](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember) interface and other generic constraints. This part of implementation contains code necessary for sending Raft-specific messages over the wire.
* Body of derived class itself. This part of implementation contains code necessary for receiving Raft-specific messages over the wire.

From architecture point of view, these two parts are separated. However, the actual implementation may require a bridge between them.

## Cluster Member
[IRaftClusterMember](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember) declares the methods that are equivalent to Raft-specific message types.

`NextIndex` property should return a location in memory to the index of the next log entry to be replicated for the current member. It doesn't contain any logic.
```csharp
using DotNext;
using DotNext.Net.Cluster.Consensus.Raft;

sealed class ClusterMember : Disposable, IRaftClusterMember
{
    private long nextIndex;

    ref long IRaftClusterMember.NextIndex => ref nextIndex;
}
```

`VoteAsync`, `PreVoteAsync`, `AppendEntriesAsync`, `InstallSnapshotAsync` are methods for sending Raft-specific messages over the wire. They are called automatically by core logic located in `RaftCluster` class. Implementation of these methods should throw [MemberUnavailableException](xref:DotNext.Net.Cluster.MemberUnavailableException) if any network-related problem occurred.

The last two methods responsible for serializing log entries to the underlying network connection. [IRaftLogEntry](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry) is inherited from [IDataTransferObject](xref:DotNext.IO.IDataTransferObject) which represents abstraction for Data Transfer Object. DTO is an object that can be serialized to or deserialized from binary form. However, serialization/deserialization process and binary layout are fully controlled by DTO itself in contrast to classic .NET serialization. You need to wrap underlying network stream to [IAsyncBinaryWriter](xref:DotNext.IO.IAsyncBinaryWriter) and pass it to `IDataTransferObject.WriteAsync` method for each log entry. `IAsyncBinaryWriter` interface has built-in static factory methods for wrapping [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter). Note that `IDataTransferObject.Length` may return **null** and you will not be able to identify log record size (in bytes) during serialization. This behavior depends on underlying implementation of Write-Ahead Log. You can examine value of `IAuditTrail.IsLogEntryLengthAlwaysPresented` property to apply necessary optimizations to the transmission process:
* If it's **true** then all log entries retrieved from such log has known size in bytes and `IDataTransferObject.Length` is not **null**. 
* If it's **false** then some or all log entries retrieved from such log has unknown size in bytes and `IDataTransferObject.Length` may be **null**. Thus, you need to provide special logic which allows to write binary data of undefined size to the underlying connection.

The default implementation for ASP.NET Core covers both cases. It uses multipart content type where log records separated by the special boundary from each other if `IAuditTrail.IsLogEntryLengthAlwaysPresented` returns **false**. Otherwise, more optimized transfer over the wire is applied. In this case, the overhead is comparable to the raw TCP connection.

`ResignAsync` method sends the message to the leader node and receiver should downgrade itself to the follower state. This is service message type not related to Raft but can be useful to force leader election.

You can use [this](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftClusterMember.cs) code as an example of HTTP-specific implementation.

## Derivation from RaftCluster
`RaftCluster` class contains all necessary methods for handling deserialized Raft messages:
* `AppendEntriesAsync` method allows to handle _AppendEntries_ Raft message type that was sent by another node
* `ResignAsync` method allows to handle leadership revocation procedure
* `InstallSnapshotAsync` method allows to handle _InstallSnapshot_ Raft message type that was sent by another node
* `VoteAsync` method allows to handle _Vote_ Raft message type that was sent by another node
* `PreVoteAsync` method allows to handle _PreVote_ message introduced as extension to original Raft model to avoid inflation of _Term_ value

The underlying code responsible for listening network requests must restore Raft messages from transport-specific representation and call the necessary handler for particular message type. 

It is recommended to use **partial class** feature of C# language to separate different parts of the derived class. The recommended layout is:
* Main part with `StartAsync` and `StopAsync` methods containing initialization logic, configuration and other infrastructure-related aspects. The example is [here](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.cs)
* Raft-related messaging. The example is [here](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.Messaging.cs)
* General-purpose messaging (if you need it)

`AppendEntriesAsync` and `InstallSnapshotAsync` expecting access to the log entries deserialized from the underlying transport. This is where [IRaftLogEntry](xref:DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry) interface comes into play. Transport-specific implementation of `IRaftLogEntry` should be present on the receiver side. Everything you need is just wrap section of underlying stream into instance of [IAsyncBinaryReader](xref:DotNext.IO.IAsyncBinaryReader) and pass the reader to [Transformation](xref:DotNext.IO.IDataTransferObject.ITransformation`1) that comes through the parameter of `TransformAsync` method. The example is [here](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/AppendEntriesMessage.cs). `IAsyncBinaryReader` has static factory methods for wrapping [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipereader).

Another important extensibility points are `StartAsync` and `StopAsync` virtual methods. They are responsible for lifecycle management of `RaftCluster` instance. You can override them for the following reasons:
* Opening and closing sockets
* Sending announcement to other nodes
* Detection of local cluster member
* Initialization of a list of cluster members
* Enforcement of configuration

## Input/Output
Low-level code related to network communication requires a choice of I/O core framework. There are two standard approaches:
* [Streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream)
* [Pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipe)

Pipe is more preferred way because of its asynchronous nature and shared memory buffer between consumer and producer. As a result, it gives you a small memory footprint during intense I/O operations. Read [this](https://docs.microsoft.com/en-us/dotnet/standard/io/pipelines) article to learn more.

.NEXT has broad support of I/O pipelines:
* `IAsyncBinaryReader.Create` static factory method can wrap [PipeReader](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipereader) to enable high-level decoding operations
* `IAsyncBinaryWriter.Create` static factory method can wrap [PipeWriter](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter) to enable high-level encoding operations
* Various [I/O enhancements](../io/index.md) aimed to simplify programming using pipes

## Network programming
The most important configuration of Raft cluster member is election timeout. Your transport-specific implementation should align socket timeouts correctly with it. For instance, connection timeout should not be greater than lower election timeout. Otherwise, you will have unstable cluster with frequent re-elections.

Another important aspect is a deduplication of Raft messages which is normal situation for TCP protocol. _Vote_, _PreVote_ and _InstallSnapshot_ are idempotent messages and can be handled twice by receiver. However, _AppendEntries_ is not.

## Hosting Model
The shape of your API for transport-specific Raft implementation depends on how the potential users will host it. There are few possible situations:
* Using Dependency Injection container:
    * Generic application host from [Microsoft.Extensions.Hosting](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting) 
    * Web host from ASP.NET Core
    * Third-party Dependency Injection container
* Standalone application without DI container

In case of DI container from Microsoft you need to implement [IHostedService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostedservice) in your derived class. The signatures of `StartAsync` and `StopAsync` methods from `RaftCluster` class are fully compatible with this interface so you don't need implement interface methods manually. As a result, you will have automatic lifecycle management and configuration infrastructure at low cost. The instance of your class which is derived from `RaftCluster` should be registered as singleton service. All its interfaces should be registered separately.

Different DI container requires correct adoption of your implementation.

If support of DI container is not a concern for you then appropriate configuration API and lifecycle management should be provided to the potential users.

The configuration of a cluster member is represented by [IClusterMemberConfiguration](xref:DotNext.Net.Cluster.Consensus.Raft.IClusterMemberConfiguration) interface. Your configuration model should be based on this interface because it should be passed to the constructor of `RaftCluster` class. Concrete implementation of the configuration model depends on the hosting model.
