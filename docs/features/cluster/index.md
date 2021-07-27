Cluster and Distributed Consensus
====
Cluster Computing is a form of distributed computing where each node set to perform the same task. The nodes usually located in the same local area network, each of them hosted on separated virtual machine or container. The input task can be distributed to the target node by load balancer or leader node. If leader node is required then cluster should use Distributed Consensus Algorithm to select exactly one leader or re-elect it if leader failed. Additionally, consensus-enabled cluster can be used to organize fault-tolerant set of microservices where only one service (leader) can be active and perform specific operations while other are in standby mode. If active node is failed then one of the standby nodes becomes active.

.NEXT cluster programming model provides the following features in addition to the core model:
1. Messaging
1. [Replication](https://en.wikipedia.org/wiki/Replication_(computing))
1. [Consensus](https://en.wikipedia.org/wiki/Consensus_(computer_science))

The programming model at higher level of abstraction is represented by interfaces:
* [IClusterMember](xref:DotNext.Net.Cluster.IClusterMember) represents individual node in the cluster
* [ICluster](xref:DotNext.Net.Cluster.ICluster) represents entire cluster. This is an entry point to work with cluster using .NEXT library.
* [IExpandableCluster](xref:DotNext.Net.Cluster.IExpandableCluster) optional interface that extends `ICluster` and represents dynamically configurable cluster where the nodes can be added or removed on-the-fly. If actual implementation doesn't support this interface then cluster can be configured only statically - it is required to shutdown entire cluster if you want to add or remove nodes
* [IMessageBus](xref:DotNext.Net.Cluster.Messaging.IMessageBus) optional interface provides message-based communication between nodes in  point-to-point manner
* [IReplicationCluster&lt;T&gt;](xref:DotNext.Net.Cluster.Replication.IReplicationCluster`1) optional interface represents a cluster where its state can be replicated across nodes to ensure consistency between them. Replication functionality based on [IAuditTrail](xref:DotNext.IO.Log.IAuditTrail). By default, design of replication infrastructure supports [Weak Consistency](https://en.wikipedia.org/wiki/Weak_consistency).

Thereby, core model consists of two interfaces: `ICluster` and `IClusterMember`. Other interfaces are extensions of the core model. 

# Messaging
Messaging feature allows to organize point-to-point communication between nodes where individual node is able to send the message to any other node. The discrete unit of communication is represented by [IMessage](xref:DotNext.Net.Cluster.Messaging.IMessage) interface which is transport- and protocol-agnostic. The actual implementation should provide protocol-specific serialization and deserialization of such messages.

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

Typed message client is represented by [MessageClient](xref:DotNext.Net.Cluster.Messaging.MessageClient) class. Its methods for sending messages are generic methods. The actual generic argument must represent DTO model describing message payload. The model must be marked with [MessageAttribute](xref:DotNext.Net.Cluster.Messaging.MessageAttribute):
```csharp
[Message("Add", MimeType = "application/octet-stream", Formatter = typeof(MessageFormatter))]
public sealed class AddMessage
{
    public int X { get; set; }
    public int Y { get; set; }
}
```

`Formatter` property points to the type that implements [IFormatter](xref:DotNext.Runtime.Serialization.IFormatter`1) interface. This type is responsible for serialization and deserialization of the message type. It must have public parameterless constructor, or public static property/field that exposes instance of this type. If field or property is presented, then its name must be specified via `FormatterMember` property of the attribute.

DTO models can be shared between the client and the listener. Sending messages via typed client is trivial:
```csharp
ISubscriber clusterMember;
var client = new MessageClient(clusterMember);
await client.SendSignal(new AddMessage { X = 40, Y = 2 }); // send one-way message
```

Typed listener must inherit from [MessageHandler](xref:DotNext.Net.Cluster.Messaging.MessageHandler) type or instantiate it using [builder](xref:DotNext.Net.Cluster.Messaging.MessageHandler.Builder). Message handling logic is represented by the public instance methods.

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

public class TestMessageHandler : MessageHandler
{
    public Task<ResultMessage> AddAsync(AddMessage message, CancellationToken token)
    {
        return Task.FromResult<ResultMessage>(new() { Result = message.X + message.Y });
    }
}
```

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
* [.NEXT Raft Suite](./raft.md) is a fully-featured implementation of Raft algorithm and related infrastructure.