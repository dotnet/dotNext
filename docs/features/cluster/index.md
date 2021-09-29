Cluster Programming Suite
====
**Cluster Computing** is a form of distributed computing where each node set to perform the same task. The nodes usually located in the same local area network, each of them hosted on separated virtual machine or container. The cluster can be organized in various ways:
* _Peer-to-peer_ where there are no special nodes that provide a service
* _Master-replica_, or _leader-follower_ where there are special _master_ node that provide a service and _replica_ nodes that contain a backup of _master_ node. Master node performs replication to keep replicas in sync. Usually, _replica_ node can replace _master_ node in case of failure. Usually, this architecture relies on Distributed Consensus Algorithm for leader election and re-election in case of leader failure. The algorithm provides a guarantee that the cluster may have exactly one leader node at a time (or zero if no quorum), linearizability of operations, consistency of data.

.NEXT cluster development suite provides the following features:
1. Messaging
1. [Rumour spreading](https://en.wikipedia.org/wiki/Gossip_protocol)
1. [Replication](https://en.wikipedia.org/wiki/Replication_(computing))
1. [Consensus](https://en.wikipedia.org/wiki/Consensus_(computer_science))
1. Cluster configuration management

The programming model at higher level of abstraction is represented by the following interfaces:
* [IPeer](xref:DotNext.Net.IPeer) represents the peer in the network
* [IPeerMesh](xref:DotNext.Net.IPeerMesh) represents a set of nodes that can communicate with each other through the network. It exposes the basic functionality for tracking of mesh events: adding or removing peers
* [IPeerMesh&lt;TPeer&gt;](xref:DotNext.Net.IPeerMesh`1) is an extension of `IPeerMesh` interface that provides access to the peer client for communication and messaging
* [IClusterMember](xref:DotNext.Net.Cluster.IClusterMember) represents an individual node in the cluster. This is an extension of Peer concept with high-level API
* [ICluster](xref:DotNext.Net.Cluster.ICluster) represents the entire cluster. This is an extension of peer mesh concept with high-level API
* [IMessageBus](xref:DotNext.Net.Cluster.Messaging.IMessageBus) optional interface provides message-based communication between nodes in  point-to-point manner
* [IReplicationCluster&lt;T&gt;](xref:DotNext.Net.Cluster.Replication.IReplicationCluster`1) optional interface represents a cluster where its state can be replicated across nodes to ensure consistency between them. Replication functionality based on [IAuditTrail](xref:DotNext.IO.Log.IAuditTrail)

Thereby, the core model consists of two interfaces: `IPeer` and `IPeerMesh`. Other interfaces are extensions of the core model.

# Messaging
Messaging feature allows to organize point-to-point communication between the nodes where individual node is able to send the message to any other node. The discrete unit of communication is represented by [IMessage](xref:DotNext.Net.Cluster.Messaging.IMessage) interface which is transport- and protocol-agnostic. The actual implementation should provide protocol-specific serialization and deserialization of such messages.

There are two types of messages:
1. **Request-Reply** message is similar to RPC call when caller should wait for the response. The response payload is represented by `IMessage`
1. **One Way** (or signal) message doesn't have response. It can be delivered in two ways:
1.1. With confirmation, when sender waiting for acknowledge from receiver side. As a result, it is possible to ensure that message is processed by receiver.
1.1. Without confirmation, when sender doesn't wait for acknowledge. Such kind of delivery is not reliable but very performant.

The message can be transferred to the particular member using [ISubscriber](xref:DotNext.Net.Cluster.Messaging.ISubscriber) interface which is the extension of `IClusterMember` interface.

Usually, you don't to implement `IMessage` interface directly due to existence of ready-to-use realizations:
1. [BinaryMessage](xref:DotNext.Net.Cluster.Messaging.BinaryMessage) for raw binary content
1. [StreamMessage](xref:DotNext.Net.Cluster.Messaging.StreamMessage) for message which payload is represented by [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream). It it suitable for large payload when it is stored on the disk
1. [TextMessage](xref:DotNext.Net.Cluster.Messaging.TextMessage) for textual content
1. [JsonMessage&lt;T&gt;](xref:DotNext.Net.Cluster.Messaging.JsonMessage`1) for JSON-serializable types

## Typed Clients and Listeners
[IMessage](xref:DotNext.Net.Cluster.Messaging.IMessage) is a low-level interface that requires a lot of boilerplate code for creating and parsing messages. It's much better to concentrate on message handling logic and hide low-level details. The same approach is used in [typed HTTP clients](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/implement-resilient-applications/use-httpclientfactory-to-implement-resilient-http-requests) in ASP.NET Core.

Typed message client or listener consists of the following parts:
* DTO models
* Serialization/deserialization logic for DTO models
* Message handling logic (for listener)

Typed message client is represented by [MessagingClient](xref:DotNext.Net.Cluster.Messaging.MessagingClient) class. Its methods for sending messages are generic methods. The actual generic argument must represent DTO model describing the message payload and serialization/deserialization logic:
```csharp
using DotNext.IO;
using DotNext.Runtime.Serialization;

public sealed class AddMessage : ISerializable<AddMessage>
{
    public const string Name = "Add";

    public int X { get; init; }
    public int Y { get; init; }

    public async ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token = default)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        await writer.WriteInt32Async(X, true, token);
        await writer.WriteInt32Async(Y, true, token);
    }

    public static async ValueTask<AddMessage> ReadFromAsync<TReader>(TReader reader, CancellationToken token = default)
        where TReader : notnull, IAsyncBinaryReader
    {
        return new AddMessage
        {
            X = await reader.ReadInt32Async(true, token),
            Y = await reader.ReadInt32Async(true, token)
        };
    }
}
```

[ISerializable&lt;TSelf&gt;](xref:DotNext.Runtime.Serialization.ISerializable`1) interface is needed to provide serialization/deserialization logic. Thanks to static abstract methods in C#, the interface requires that the implementing type must provide deserialization logic in the form of static factory method.

DTO models can be shared between the client and the listener. The message type must be registered in the client. After that, sending messages via typed client is trivial:
```csharp
ISubscriber clusterMember;
var client = new MessagingClient(clusterMember).RegisterMessage<AddMessage>(AddMessage.Name);
await client.SendSignal(new AddMessage { X = 40, Y = 2 }); // send one-way message
```

Typed listener must inherit from [MessageHandler](xref:DotNext.Net.Cluster.Messaging.MessageHandler) type or instantiated using [builder](xref:DotNext.Net.Cluster.Messaging.MessageHandler.Builder). Message handling logic is represented by the public instance methods.

For duplex (request-reply) message handler the method must follow to one of the allowed signatures:
```csharp
Task<OutputMessage> MethodName(InputMessage input, CancellationToken token);

Task<OutputMessage> MethodName(ISubscriber sender, InputMessage input, CancellationToken token);

Task<OutputMessage> MethodName(InputMessage input, object? context, CancellationToken token);

Task<OutputMessage> MethodName(ISubscriber sender, InputMessage input, object? context, CancellationToken token);
```

For one-way message handler the method must follow to one of the allowed signatures:
```csharp
Task MethodName(InputMessage input, CancellationToken token);

Task MethodName(ISubscriber sender, InputMessage input, CancellationToken token);

Task MethodName(InputMessage input, object? context, CancellationToken token);

Task MethodName(ISubscriber sender, InputMessage input, object? context, CancellationToken token);
```

`InputMessage` is DTO model for the message. _sender_ parameter allows to obtained information about message sender. _context_ parameter supplies extra information about underlying transport for the message.

The following example demonstrates declaration of typed message listener:
```csharp
using DotNext.Net.Cluster.Messaging;
using System.Threading;
using System.Threading.Tasks;

[Message<AddMessage>(AddMessage.Name)]
public class TestMessageHandler : MessageHandler
{
    public Task<ResultMessage> AddAsync(AddMessage message, CancellationToken token)
    {
        return Task.FromResult<ResultMessage>(new() { Result = message.X + message.Y });
    }
}
```

In contrast to `MessagingClient`, all message types must be registered using [MessageAttribute&lt;TMessage&gt;](xref:DotNext.Net.Cluster.Messaging.MessageAttribute`1) attribute declaratively. However, this is not applicable when you constructing the handle using [builder](xref:DotNext.Net.Cluster.Messaging.MessageHandler.Builder).

# Rumour Spreading
Gossip-based messaging provides scalable way to broadcast messages across all cluster nodes. [IPeerMesh](xref:DotNext.Net.IPeerMesh) exposes the basic functionality to discover the peers visible from the local node. The key aspect of gossiping is ability to discover neighbors. This capability is usually called _membership protocol_ for Gossip-based communication. There are few approaches to achieve that:
* [HyParView](https://asc.di.fct.unl.pt/~jleitao/pdf/dsn07-leitao.pdf) for large-scale peer meshes with hundreds or event thousands of peers
* [SWIM](https://research.cs.cornell.edu/projects/Quicksilver/public_pdfs/SWIM.pdf) for mid-size clusters where each node has weakly-consistent view of the entire cluster

Currently, .NEXT offers HyParView implementation only. Read more about Gossip-based communication with .NEXT in this [article](./gossip.md).

If you want to know more about infection-style communication in cluster computing then use the following links:
* [Introduction to Gossip protocols](https://managementfromscratch.wordpress.com/2016/04/01/introduction-to-gossip/)
* [Gossip Simulator](https://flopezluis.github.io/gossip-simulator/)
* [Make your cluster SWIM](https://bartoszsypytkowski.com/make-your-cluster-swim/)
* [HyParView: cluster membership that scales](https://bartoszsypytkowski.com/hyparview/)

# Distributed Consensus
Consensus Algorithm allows to achieve overall reliability in the presence of faulty nodes. The most commonly used consensus algorithms are:
* [Chandra-Toueg consensus algorithm](https://en.wikipedia.org/wiki/Chandra%E2%80%93Toueg_consensus_algorithm)
* [Paxos](https://en.wikipedia.org/wiki/Paxos_(computer_science))
* [Raft](https://en.wikipedia.org/wiki/Raft_(computer_science))

The consensus algorithm allows to choose exactly one leader node in the cluster.

.NEXT library provides protocol-agnostic implementation of Raft algorithm that can be adopted for any real network protocol. You can reuse this implementation which is located in `DotNext.Net.Cluster.Consensus.Raft` namespace. If you want to know more about Raft then use the following links:
* [The Raft Consensus Algorithm](https://raft.github.io/)
* [The Secret Lives of Data](http://thesecretlivesofdata.com/)
* [In Search of an Understandable Consensus Algorithm](https://raft.github.io/raft.pdf)
* [Dissertation](https://github.com/ongardie/dissertation)

# Replication
Replication allows to share information between nodes to ensure consistency between them. Usually, consensus algorithm covers replication process. In .NEXT library, replication functionality relies on the fact that each cluster node has its own persistent audit trail (or transaction log). However, the only default implementation of it is in-memory log which is suitable in siutations when your distributed application requires distributed consensus only and don't have distributed state that should be synchronized across cluster. If you need reliable replication then provide your own implementation of [IAuditTrail&lt;T&gt;](xref:DotNext.IO.Log.IAuditTrail`1) interface or use [PersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState) class.

[IReplicationCluster](xref:DotNext.Net.Cluster.Replication.IReplicationCluster) interface indicates that the specific cluster implementation supports state replication across cluster nodes. It exposed access to the audit trail used to track local changes and commits on other cluster nodes.

# Implementations
* [.NEXT Raft](./raft.md) is a fully-featured implementation of Raft algorithm and related infrastructure
* [.NEXT HyParView](./gossip.md) is a fully-featured implementation of HyParView membership protocol for reliable Gossip-based communication