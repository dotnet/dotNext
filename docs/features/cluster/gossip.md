Gossip-based Communication
====
[Gossip-based communication](https://en.wikipedia.org/wiki/Gossip_protocol) is a way to spread a message (rumour) across all cluster nodes using infection-style message exchange. This approach is very efficient in comparison to naive broadcasting especially in large-scale clusters. But how the node knows how to join the cluster and discover other peers? The answer is _membership protocol_ that allows to discover and keep the view of cluster members.

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

`EnqueueBroadcastAsync` method can be used to broadcast the message (rumour) to all neighbors. This is a core of Gossip-based messaging. The method requires an implementation of [IRumourSender](xref:DotNext.Net.Cluster.Messaging.Gossip.IRumourSender) interface that provides the delivery of the message using the specific transport. Additionally, the method controls the delivery status. If it suspects that the peer is unavailable then it removes the peer from the list of neighbors.

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
* [PeerController](xref:DotNext.Net.Cluster.Discovery.HyParView.PeerController) maintains the state of the local peer, handles HyParView-specific messages and exposes `EnqueueBroadcastAsync` method for rumour spreading

### Configuration
The application should be configured properly to serve HyParView messages. The following JSON represents the example of configuration:
```json
{
    "resourcePath" : "/membership/hyparview",
    "protocolVersion" : "http2",
    "protocolVersionPolicy" : "RequestVersionOrLower",
    "requestTimeout" : "00:01:00",
    "clientHandlerName" : "HyParViewClient",
    
}
```

## Example