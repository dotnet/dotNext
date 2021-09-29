Gossip-based Communication
====
[Gossip-based communication](https://en.wikipedia.org/wiki/Gossip_protocol) is a way to spread a message (rumor) across all cluster nodes using infection-style message exchange. This approach is very efficient in comparison to naive broadcasting especially in large-scale clusters. But how the node knows how to join the cluster and discover other peers? The answer is _membership protocol_ that allows to discover and keep the view of cluster members.

# HyParView
[HyParView](https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf) is an implementation of _membership protocol_ for peers that want to communicate with each other using infection-style message exchange. This algorithm is highly scalable up to thousands of nodes. However, it has one major drawback: every node has partial view of the entire cluster.

.NEXT offers transport-agnostic implementation of HyParView algorithm represented by [PeerController](xref:DotNext.Net.Cluster.Discovery.HyParView.PeerController) class. It's an entry point for building a peer with messaging capabilities. The controller is responsible for all key aspects of the algorithm:
* Implementation of joining procedure
* Maintaining a list of neighbors
* Announcing of new peers
* Removing peers from the mesh

[IPeerConfiguration](xref:DotNext.Net.Cluster.Discovery.HyParView.IPeerConfiguration) interface provides the configuration model of HyParView-enabled peer. When the peer configured properly, you need to bootstrap the controller using `StartAsync` method. The method accepts an address of the _contact node_ optionally. This address is not needed when you starting the first peer in the mesh. However, if you already have a mesh of peers then you need to announce a new peer correctly. _Contact node_ is responsible for announcing joined peer across all peers in the mesh. There is no preference in choosing the appropriate node for that purpose.

When the node is joined to the mesh then it will be able to discover a list of neighbors automatically. `PeerDiscovered` and `PeerGone` events of `PeerController` class provide ability to track changes in the list of visible peers. `Neighbors` property provides a list of the peers visible by the local node.

`StopAsync` method provides a way for graceful shutdown of the node. All neighbors will be informed that the stopped node is no longer accessible and it must be removed from the list of neighbors.

`EnqueueBroadcastAsync` method can be used to broadcast the message (rumor) to all neighbors. This is a core of Gossip-based messaging. The method requires an implementation of [IRumourSender](xref:DotNext.Net.Cluster.Messaging.Gossip.IRumourSender) interface that provides the delivery of the message using the specific transport. Additionally, the method controls the delivery status. If it suspects that the peer is unavailable then it removes the peer from the list of neighbors.

## Integration with ASP.NET Core
`DotNext.AspNetCore.Cluster` provides implementation of HyParView protocol using HTTP transport and [PeerController](xref:DotNext.Net.Cluster.Discovery.HyParView.PeerController) class on the top of ASP.NET Core infrastructure.

There are two main components of HyParView implementation that must be registered in ASP.NET Core application:
* Singleton service that implements [PeerController](xref:DotNext.Net.Cluster.Discovery.HyParView.PeerController) abstract class and maintain the state of the peer
* ASP.NET Core Middleware that is responsible for processing HyParView messages over HTTP

The following code demonstrates a basic setup of HyParView:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

sealed class Startup
{
    public void Configure(IApplicationBuilder app)
    {
        app.UseHyParViewProtocolHandler();	// informs that processing pipeline should handle HyParView-specific requests
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions();
    }
}

IHost host = new HostBuilder()
    .ConfigureWebHost(webHost => webHost
        .UseKestrel(options => options.ListenLocalhost(80))
        .UseStartup<Startup>()
    )
    .JoinMesh()  //registers all necessary services required for normal cluster node operation
    .Build();
```

Note that `JoinMesh` method should be called after `ConfigureWebHost`. Otherwise, the behavior of this method is undefined.

`JoinMesh` method has overloads that allow to specify custom configuration section containing the configuration of the local peer.

`UseHyParViewProtocolHandler` method should be called before registration of any authentication/authorization middleware.

### Dependency Injection
The application may request the following services from ASP.NET Core DI container:
* [IPeerMesh&lt;HttpPeerClient&gt;](xref:DotNext.Net.IPeerMesh`1) provides access to all peers visible from the local node, discovery events, configured [HttpClient](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) for communication with neighbors
* [PeerController](xref:DotNext.Net.Cluster.Discovery.HyParView.PeerController) maintains the state of the local peer, handles HyParView-specific messages and exposes `EnqueueBroadcastAsync` method for rumor spreading

### Configuration
The application should be configured properly to serve HyParView messages. The following JSON represents the example of configuration:
```json
{
    "resourcePath" : "/membership/hyparview",
    "protocolVersion" : "http2",
    "protocolVersionPolicy" : "RequestVersionOrLower",
    "requestTimeout" : "00:01:00",
    "clientHandlerName" : "HyParViewClient",
    "contactNode" : "https://192.168.0.2:3232/",
    "localNode" : "https://192.168.0.1:3232",
    "activeViewCapacity" : 5,
    "passiveViewCapacity" : 10,
    "activeRandomWalkLength" : 3,
    "passiveRandomWalkLength" : 2,
    "shuffleActiveViewCount" : 2,
    "shufflePassiveViewCount" : 5,
    "shuffleRandomWalkLength" : 2,
    "queueCapacity" : 15,
    "lowerShufflePeriod" : 1000,
    "upperShufflePeriod" : 1500,
}
```

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| resourcePath | No | /membership/hyparview | The relative path to the endpoint responsible for handling internal HyParView messages |
| protocolVersion | No | auto | HTTP protocol version to be used for the communication between members. Possible values are `auto`, `http1`, `http2`, `http3` |
| protocolVersionPolicy | No | RequestVersionOrLower | Specifies behaviors for selecting and negotiating the HTTP version for a request. Possible values are `RequestVersionExact`, `RequestVersionOrHigher`, `RequestVersionOrLower`
| requestTimeout | No | 00:30:00 | Request timeout used to access peers across the network using HTTP client |
| clientHandlerName | No | HyParViewClient | The name to be passed into [IHttpMessageHandlerFactory](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.ihttpmessagehandlerfactory) to create [HttpMessageInvoker](https://docs.microsoft.com/en-us/dotnet/api/system.net.http.httpmessageinvoker) used by HyParView client code |
| contactNode | No | N/A | The address of the contact node used as an entry point to announce the local node. This property should not be specified if the local node is the first node launched as a part of the mesh |
| localNode | Yes | N/A | The addres of the local node visible by other peers in the mesh |
| activeViewCapacity | No | 5 | The maximum number of neighbors visible by the local node. The recommended value of this property can be calculated as _log(n) + c_, where _n_ is a maximum  number of peers, _c_ is a constant value. According to HyParView paper, the recommended value of _c_ is 1 |
| passiveViewCapacity | No | 10 | The maximum number of peers stored in the backlog that is used to replace the peers removed from the active view. The recommended value of this property can be calculated as _k(log(n) + c)_, where _k_ is a constant value. According to HyParView paper, the recommended value of _k_ is 6 |
| activeRandomWalkLength | No | 3 | The maximum number of hops a _ForwardJoin_ request is propagated |
| passiveRandomWalkLength | No | 2 | The value specifies at which point in the walk the peer is inserted into passive view. The value should be less than `activeRandomWalkLength` configuration property |
| shuffleActiveViewCount | No | `activeViewCapacity / 2` | The number of peers from active view to be included into _Shuffle_ message |
| shufflePassiveViewCount | No | `shufflePassiveViewCount / 2` | The number of peers from passive view to be included into Shuffle message |
| shuffleRandomWalkLength | No | `passiveRandomWalkLength` | The maximum number of hops a Shuffle message is propagated |
| queueCapacity | No | `activeViewCapacity + passiveViewCapacity` | The capacity of the internal queue used to process HyParView messages |
| lowerShufflePeriod | No | 1000 | The lower bound of randomly selected shuffle period, in milliseconds |
| upperShufflePeriod | No | 3000 | The upper bound of randomly selected shuffle period |

If _lowerShufflePeriod_ &lt; _upperShufflePeriod_ then the actual period will be chosen randomly in the specified range. If _lowerShufflePeriod = upperShufflePeriod_ then the actual period is determined by a constant value. Otherwise, the controller never sends _Shuffle_ message to other peers.

### Controlling node lifetime
The service implementing `PeerController` abstract class is registered as singleton service. It starts receiving HyParView-specific messages immediately. Therefore, you can loose some events raised by the service such as `PeerDiscovered` at starting point. To avoid that, you can implement [IPeerLifetime](xref:DotNext.Net.Cluster.Discovery.HyParView.IPeerLifetime) interface and register implementation as a singleton.

```csharp
using DotNext.Net;
using DotNext.Net.Cluster.Discovery.HyParView;

internal sealed class MemberLifetime : IPeerLifetime
{
	private static void OnPeerDiscovered(PeerController controller, PeerEventArgs args) {}

	void IPeerLifetime.OnStart(PeerController controller)
	{
		controller.PeerDiscovered += OnPeerDiscovered;
	}

	void IPeerLifetime.OnStop(PeerController controller)
	{
		controller.PeerDiscovered -= OnPeerDiscovered;
	}
}
```

## Example
There is HyParView playground represented by HyParViewPeer application. You can find this app [here](https://github.com/dotnet/dotNext/tree/master/src/examples/HyParViewPeer). This playground allows to test HyParView membership protocol in real world.

The following command starts the first peer in the mesh:
```bash
cd <dotnext>/src/examples/HyParViewPeer
dotnet run -- 3262
```

_3262_ is a port number that can be used to access the peer. When a mesh has at least one launched peer, you can select any peer as a contact node and specify its port when starting a new node:
```bash
cd <dotnext>/src/examples/HyParViewPeer
dotnet run -- 3263 3262
```
_3263_ is a port of the started node. The second argument (port _3262_) specifies the port of the contact node. This node must be launched.

You can launch as many peers as you want, but the port number must be unique for each instance.

The terminal window of the peer will display discovery events. Each peer exposes the following HTTP resources that can be examined using Web Browser:
* GET _https://localhost:3262/neighbors_ can be used to obtain a list of neighbors visible by the peer. You can change _3262_ to the port number of the appropriate peer. According to HyParView, each peer may have partial view of the entire cluster so the list of neighbors may differ
* GET _https://localhost:3262/rumor_ can be used to spread the rumor across all peers in the mesh. The terminal window associated with each launched peer will display the identifier of the broadcast message