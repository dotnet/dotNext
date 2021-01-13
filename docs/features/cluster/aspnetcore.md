Clustered ASP.NET Core Microservices
====
.NEXT provides fully-featured implementation of cluster computing infrastructure for microservices constructed on top of ASP.NET Core. This implementation consists of the following features:
* Point-to-point messaging between microservices organized through HTTP
* Consensus algorithm is Raft and all necessary communication for this algorithm is based on HTTP
* Replication according with Raft algorithm is fully supported. In-memory audit trail is used by default.

In this implementation, Web application treated as cluster node. The following example demonstrates how to turn ASP.NET Core application into cluster node:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class Startup
{
    private readonly IConfiguration configuration;

    public Startup(IConfiguration configuration) => this.configuration = configuration;

    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler();	//informs that processing pipeline should handle Raft-specific requests
    }

    public void ConfigureServices(IServiceCollection services)
    {
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

Raft algorithm requires dedicated HTTP endpoint for internal purposes. There are two possible ways to expose necessary endpoint:
* **Hosted Mode** exposes internal endpoint at different port because dedicated [Web Host](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.iwebhost) is used
* **Embedded Mode** exposes internal endpoint at the same port as underlying web application

The necessary mode depends on your requirements and network environment.

# Dependency Injection
Web application component can request the following service from ASP.NET Core DI container:
* [ICluster](../../api/DotNext.Net.Cluster.ICluster.yml)
* [IRaftCluster](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftCluster.yml) represents Raft-specific version of `ICluster` interface
* [IMessageBus](../../api/DotNext.Net.Cluster.Messaging.IMessageBus.yml) for point-to-point messaging between nodes
* [IExpandableCluster](../../api/DotNext.Net.Cluster.ICluster.yml) for tracking changes in cluster membership
* [IReplicationCluster&lt;IRaftLogEntry&gt;](../../api/DotNext.Net.Cluster.Replication.IReplicationCluster-1.yml) to work with audit trail used for replication. [IRaftLogEntry](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry.yml) is Raft-specific representation of the record in the audit trail
* [IReplicationCluster](../../api/DotNext.Net.Cluster.Replication.IReplicationCluster.yml) to work with audit trail in simplified manner 

# Configuration
The application should be configured properly to work as a cluster node. The following JSON represents example of configuration:
```json
{
	"partitioning" : false,
	"lowerElectionTimeout" : 150,
	"upperElectionTimeout" : 300,
	"members" : ["http://localhost:3262", "http://localhost:3263", "http://localhost:3264"],
	"metadata" :
	{
		"key": "value"
	},
	"allowedNetworks" : ["127.0.0.0", "255.255.0.0/16", "2001:0db9::1/64"],
	"hostAddressHint" : "192.168.0.1",
	"requestJournal" :
	{
		"memoryLimit": 5,
		"expiration": "00:00:10",
		"pollingInterval" : "00:01:00"
	},
	"resourcePath" : "/cluster-consensus/raft",
	"port" : 3262,
	"heartbeatThreshold" : 0.5,
    "requestTimeout" : "00:01:00",
    "keepAliveTimeout": "00:02:00",
    "requestHeadersTimeout" : "00:00:30"
}
```

| Configuration parameter | Mode | Required | Default Value | Description |
| ---- | ---- | ---- | ---- | -- |
| partitioning | Hosted, Embedded | No | false | `true` if partitioning supported. In this case, each cluster partition may have its own leader, i.e. it is possible to have more that one leader for external observer. However, single partition cannot have more than 1 leader. `false` if partitioning is not supported and only one partition with majority of nodes can have leader. Note that cluster may be totally unavailable even if there are operating members presented
| lowerElectionTimeout, upperElectionTimeout | Hosted, Embedded | No | 150, 300 |  Defines range for election timeout (in milliseconds) which is picked randomly inside of it for each cluster member. If cluster node doesn't receive heartbeat from leader node during this timeout then it becomes a candidate and start a election. The recommended value for  _upperElectionTimeout_ is `2  X lowerElectionTimeout`
| members | Hosted, Embedded | Yes | N/A | An array of all cluster nodes. This list must include local node. DNS name cannot be used as host name in URL except `localhost`. Only IP address is allowed |
| allowedNetworks | Hosted, Embedded | No | Empty list which means that all networks are allowed | List of networks with other nodes which a part of single cluster. This property can be used to restrict unathorized requests to the internal endpoint responsible for handling Raft messages |
| metadata | Hosted, Embedded | No | empty dictionary | A set of key/value pairs to be associated with cluster node. The metadata is queriable through `IClusterMember` interface |
| openConnectionForEachRequest | Hosted, Embedded | No | false | `true` to create TCP connection every time for each outbound request. `false` to use HTTP KeepAlive |
| clientHandlerName | Hosted, Embedded | No | raftClient | The name to be passed into [IHttpMessageHandlerFactory](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.ihttpmessagehandlerfactory) to create [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker) used by Raft client code |
| resourcePath | Embedded | No | /cluster-consensus/raft | Defines relative path to the endpoint responsible for handling internal Raft messages |
| port | Hosted | No | 32999 | Defines the port number that the internal endpoint handler is listening to.
| requestJournal:memoryLimit | Hosted, Embedded | No | 10 | The maximum amount of memory (in MB) utilized by internal buffer used to track duplicate messages |
| requestJournal:expiration | Hosted, Embedded | No | 00:00:10 | The eviction time of the record containing unique request identifier |
| requestJournal:pollingInterval | Hosted, Embedded | No | 00:01:00 | Gets the maximum time after which the buffer updates its memory statistics |
| hostAddressHint | Hosted, Embedded | No | N/A | Allows to specify real IP address of the host where cluster node launched. Usually it is needed when node executed inside of Docker container. If this parameter is not specified then cluster node may fail to detect itself because network interfaces inside of Docker container have different addresses in comparison with real host network interfaces. The value can be defined at container startup time, e.g. `docker container run -e "member-config:hostAddressHint=$(hostname -i)"` |
| heartbeatThreshold | Hosted, Embedded | No | 0.5 | Specifies frequency of heartbeat messages generated by leader node to inform follower nodes about its leadership. The range is (0, 1). The lower the value means that the messages are generated more frequently and vice versa. |
| protocolVersion | Hosted, Embedded | No | `auto` | HTTP protocol version that should be used for communication between members. Possible values are `auto`, `http1`, `http2`, `http3` |
| requestHeadersTimeout | Hosted | No | 30 seconds | The maximum amount of time the server will spend receiving request headers |
| keepAliveTimeout | Hosted | No | 2 minutes | TCP keep-alive timeout |
| requestTimeout | Hosted, Embedded | No | `upperElectionTimeout` | Request timeout used to access cluster members across the network using HTTP client |
| rpcTimeout | Hosted, Embedded | No | `upperElectionTimeout` / 2 | Request timeout used to send Raft-specific messages to cluster members. Must be less than or equal to _requestTimeout_ parameter |
| standby | Hosted, Embedded | No | false | **true** to prevent election of the cluster member as a leader. It's useful to configure nodes available for read-only operations |

`requestJournal` configuration section is rarely used and useful for high-load scenario only.

> [!NOTE]
> Usually, real-world ASP.NET Core application hosted on `0.0.0.0`(IPv4) or `::`(IPv6). When testing locally, use explicit loopback IP instead of `localhost` as host name for all nodes in `members` section.

Choose `lowerElectionTimeout` and `upperElectionTimeout` according with the quality of your network. If values are small then you get frequent elections and migration of leader node.

## Runtime Hook
The service implementing `IRaftCluster` is registered as singleton service. The service starts receiving Raft-specific messages immediately. Therefore, you can loose some events raised by the service such as `LeaderChanged` at starting point. To avoid that, you can implement [IClusterMemberLifetime](../../api/DotNext.Net.Cluster.Consensus.Raft.IClusterMemberLifetime.yml) interface and register implementation as singleton.

```csharp
using DotNext.Net.Cluster.Consensus.Raft;
using System.Collections.Generic;

internal sealed class MemberLifetime : IClusterMemberLifetime
{
	private static void LeaderChanged(ICluster cluster, IClusterMember leader) {}

	void IClusterMemberLifetime.Initialize(IRaftCluster cluster, IDictionary<string, string> metadata)
	{
		metadata["key"] = "value";
		cluster.LeaderChanged += LeaderChanged;
	}

	void IClusterMemberLifetime.Shutdown(IRaftCluster cluster)
	{
		cluster.LeaderChanged -= LeaderChanged;
	}
}
```

Additionally, the hook can be used to modify metadata of the local cluster member.

## HTTP Client Behavior
.NEXT uses [HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) for communication between cluster nodes. The client itself delegates all operations to [HttpMessageHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler). It's not recommended to use [HttpClientHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclienthandler) because it has inconsistent behavior on different platforms because relies on _libcurl_. Raft implementation uses `Timeout` property of `HttpClient` to establish request timeout. It is always defined as `upperElectionTimeout` by .NEXT infrastructure. To demonstrate inconsistent behavior let's introduce three cluster nodes: _A_, _B_ and _C_. _A_ and _B_ have been started except _C_:
* On Windows the leader will not be elected even though the majority is present - 2 of 3 nodes are available. This is happening because Connection Timeout is equal to Response Timeout, which is equal to `upperElectionTimeout`.
* On Linux everything is fine because Connection Timeout less than Response Timeout

By default, Raft implementation uses [SocketsHttpHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler). However, the handler can be overridden using [IHttpMessageHandlerFactory](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.ihttpmessagehandlerfactory). You can implement this interface manually and register its implementation as singleton. .NEXT tries to use this interface if it is registered as a factory of custom [HttpMessageHandler](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessagehandler). The following example demonstrates how to implement this interface and create platform-independent version of message invoker:

```csharp
using System;
using System.Net.Http;

internal sealed class RaftClientHandlerFactory : IHttpMessageHandlerFactory
{
	public HttpMessageHandler CreateHandler(string name) => new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromMilliseconds(100) };
}
```

In practice, `ConnectTimeout` should be equal to `lowerElectionTimeout` configuration property. Note that `name` parameter is equal to the `clientHandlerName` configuration property when handler creation is requested by Raft implementation.

# Hosted Mode
This mode allows to create separated [Web Host](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.iwebhost) used for hosting Raft-specific stuff. As a result, Raft implementation listens on the port that differs from the port of underlying Web application. 

The following example demonstrates this approach:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

IHost host = new HostBuilder()
    .ConfigureWebHost(webHost => webHost
        .UseKestrel(options => options.ListenLocalhost(80))
        .UseStartup<Startup>()
    )
    .JoinCluster()  //registers all necessary services required for normal cluster node operation
    .Build();
```

Note that `JoinCluster` method declared in [DotNext.Net.Cluster.Consensus.Raft.Http.Hosting](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.Hosting.yml) namespace and should be called after `ConfigureWebHost`. Otherwise, the behavior of this method is undefined.

By default, .NEXT uses Kestrel web server to serve Raft requests. However, it is possible to configure dedicated host manually. To do that, you need to register singleton service implementing [IDedicatedHostBuilder](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.Hosting.IDedicatedHostBuilder.yml) interface. In this case, `port` configuration property will be ignored.

`JoinCluster` method has overloads that allow to specify custom configuration section containing configuration of local node.

# Embedded Mode
Embedded mode shares the same [Web Host](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.hosting.iwebhost) and port with underlying Web Application. To serve Raft-specific requests the implementation uses dedicated endpoint `/cluster-consensus/raft` that can be changed through configuration parameter. The following example demonstrates how to setup embedded mode:

```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
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
        app.UseConsensusProtocolHandler();	//informs that processing pipeline should handle Raft-specific requests
    }

    public void ConfigureServices(IServiceCollection services)
    {
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

Note that `JoinCluster` declared in [DotNext.Net.Cluster.Consensus.Raft.Http.Embedding](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.Embedding.yml) namespace and should be called after `ConfigureWebHost`. Otherwise, the behavior of this method is undefined.

`JoinCluster` method has overloads that allow to specify custom configuration section containing configuration of local node.

`UseConsensusProtocolHandler` method should be called before registration of any authentication/authorization middleware.

# Redirection to Leader
Now cluster of ASP.NET Core applications can receive requests from outside. Some of these requests may be handled by leader node only. .NEXT cluster programming model provides a way to automatically redirect request to leader node if it was originally received by follower node. The redirection is organized with help of _307 Temporary Redirect_ status code. Every follower node knows the actual address of the leader node. If cluster or its partition doesn't have leader then node returns _503 Service Unavailable_. 

Automatic redirection is provided by [LeaderRouter](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.LeaderRouter.yml) class. You can specify endpoint that should be handled by leader node with `RedirectToLeader` method.

```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
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

This redirection can be transparent to actual client if you use reverse proxy server such as NGINX. Reverse proxy can automatically handle redirection without returning control to the client.

## Custom Redirections
It is possible to change default behavior of redirection where _301 Moved Permanently_ status code is used. You can pass custom implementation into the optional parameter of `RedirectToLeader` method.

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

## Port mapping
Redirection mechanism trying to construct valid URI of the leader node based on its actual IP address. Identification of the address is not a problem unlike port number. The infrastructure cannot use the port if its [WebHost](https://docs.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.webhost) because of Hosted Mode or the port from the incoming `Host` header because it can be rewritten by reverse proxy. The only way is to use the inbound port of the TCP listener responsible for handling all incoming HTTP requests. It is valid for the non-containerized environment. Inside of the container the ASP.NET Core application port is mapped to the externally visible port which not always the same. In this case you can specify port for redirections explicitly as follows:

```csharp
public void Configure(IApplicationBuilder app)
{
    app.UseConsensusProtocolHandler()
      .RedirectToLeader("/endpoint1", applicationPortHint: 3265);
}
```

# Messaging
.NEXT extension for ASP.NET Core supports messaging beween nodes through HTTP out-of-the-box. However, the infrastructure don't know how to handle custom messages. Therefore, if you want to utilize this functionality then you need to implement [IInputChannel](../../api/DotNext.Net.Cluster.Messaging.IInputChannel.yml) interface.

Messaging inside of cluster supports redirection to the leader as well as for external client. But this mechanism implemented differently and exposed as `IInputChannel` interface via [IMessageBus.LeaderRouter](../../api/DotNext.Net.Cluster.Messaging.IMessageBus.yml) property.

# Replication
Raft algorithm requires additional persistent state in order to basic audit trail. This state is represented by [IPersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.IPersistentState.yml) interface. By default, it is implemented as [in-memory storage](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.InMemoryAuditTrail.html) which is suitable only for applications that doesn't have replicated state. If your application has it then use [PersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml) class or implement this interface manually and use reliable storage such as disk. The implementation can be injected explicitly via `AuditTrail` property of [IRaftCluster](../../api/DotNext.Net.Cluster.Consensus.Raft.IRaftCluster.yml) interface or implicitly via Dependency Injection. The explicit should be done inside of the user-defined implementation of [IClusterMemberLifetime](../../api/DotNext.Net.Cluster.Consensus.Raft.IClusterMemberLifetime.yml) interface registered as a singleton service in ASP.NET Core application. The implicit injection requires registration of singleton service which implements [IPersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.IPersistentState.yml) interface.

## Reliable State
Information about reliable persistent state which uses disk for storing write ahead log located in the separated [article](./wal.md). However, its usage turns your microservice into stateful service because its state persisted on disk. Consider this fact if you are using containerization technologies such as Docker or LXC.

# Metrics
It is possible to measure runtime metrics of Raft node internals using [HttpMetricsCollector](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.Http.HttpMetricsCollector.html) class. The reporting mechanism is agnostic  to the underlying metrics delivery library such as [AppMetrics](https://github.com/AppMetrics/AppMetrics).

The class contains methods that are called automatically. You can override them and implement necessary reporting logic. By default, these methods do nothing.

The metrics collector should be registered as singleton service using Dependency Injection. However, the type of the service used for registration should of [MetricsCollector](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.MetricsCollector.html) type.

```csharp
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
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

It is possible to derive directly from [MetricsCollector](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.MetricsCollector.html) if you don't need to receive metrics related to HTTP-specific implementation of Raft algorithm.

Implementation of reporting method should fast as possible or asynchronous. If reporting causes I/O operations synchronously then it affects the overall performance of Cluster library internals such as communication with other cluster members which is time-critical.

# Example
There is Raft playground represented by RaftNode application. You can find this app [here](https://github.com/sakno/dotNext/tree/develop/src/examples/RaftNode). This playground allows to test Raft consensus protocol in real world. Each instance of launched application represents cluster node. All nodes can be started using the following script:
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
