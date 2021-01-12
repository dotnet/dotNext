Cluster and Distributed Consensus
====
Cluster Computing is a form of distributed computing where each node set to perform the same task. The nodes usually located in the same local area network, each of them hosted on separated virtual machine or container. The input task can be distributed to the target node by load balancer or leader node. If leader node is required then cluster should use Distributed Consensus Algorithm to select exactly one leader or re-elect it if leader failed. Additionally, consensus-enabled cluster can be used to organize fault-tolerant set of microservices where only one service (leader) can be active and perform specific operations while other are in standby mode. If active node is failed then one of the standby nodes becomes active.

.NEXT cluster programming model provides the following features in addition to the core model:
1. Messaging
1. [Replication](https://en.wikipedia.org/wiki/Replication_(computing))
1. [Consensus](https://en.wikipedia.org/wiki/Consensus_(computer_science))

The programming model at higher level of abstraction is represented by interfaces:
* [IClusterMember](../../api/DotNext.Net.Cluster.IClusterMember.yml) represents individual node in the cluster
* [ICluster](../../api/DotNext.Net.Cluster.ICluster.yml) represents entire cluster. This is an entry point to work with cluster using .NEXT library.
* [IExpandableCluster](../../api/DotNext.Net.Cluster.IExpandableCluster.yml) optional interface that extends `ICluster` and represents dynamically configurable cluster where the nodes can be added or removed on-the-fly. If actual implementation doesn't support this interface then cluster can be configured only statically - it is required to shutdown entire cluster if you want to add or remove nodes
* [IMessageBus](../../api/DotNext.Net.Cluster.Messaging.IMessageBus.yml) optional interface provides message-based communication between nodes in  point-to-point manner
* [IReplicationCluster](../../api/DotNext.Net.Cluster.Replication.IReplicationCluster-1.yml) optional interface represents a cluster where its state can be replicated across nodes to ensure consistency between them. Replication functionality based on [audit trail](../../api/DotNext.Net.Cluster.Replication.IAuditTrail-1.yml). By default, design of replication infrastructure supports [Weak Consistency](https://en.wikipedia.org/wiki/Weak_consistency).

Thereby, core model consists of two interfaces: `ICluster` and `IClusterMember`. Other interfaces are extensions of the core model. 

# Messaging
Messaging feature allows to organize point-to-point communication between nodes where individual node is able to send the message to any other node. The discrete unit of communication is represented by [IMessage](../../api/DotNext.Net.Cluster.Messaging.IMessage.yml) interface which is transport- and protocol-agnostic. The actual implementation should provide protocol-specific serialization and deserialization of such messages.

There are two types of messages:
1. **Request-Reply** message is similar to RPC call when caller should wait for the response. The response payload is represented by `IMessage`
1. **One Way** (or signal) message doesn't have response. It can be delivered in two ways:
1.1. With confirmation, when sender waiting for acknowledge from receiver side. As a result, it is possible to ensure that message is processed by receiver.
1.1. Without confirmation, when sender doesn't wait for acknowledge. Such kind of delivery is not reliable but very performant.

The message can be transferred to the particular member using [ISubscriber](../../api/DotNext.Net.Cluster.Messaging.ISubscriber.yml) interface which is the extension of `IClusterMember` interface.

Usually, you don't to implement `IMessage` interface directly due to existence of ready-to-use realizations:
1. [BinaryMessage](../../api/DotNext.Net.Cluster.Messaging.BinaryMessage.yml) for raw binary content
1. [StreamMessage](../../api/DotNext.Net.Cluster.Messaging.StreamMessage.yml) for message which payload is represented by [Stream](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream). It it suitable for large payload when it is stored on the disk
1. [TextMessage](../../api/DotNext.Net.Cluster.Messaging.TextMessage.yml) for textual content

# Distributed Consensus
Consensus Algorithm allows to achieve overall reliability in the presence of faulty nodes. The most commonly used consensus algorithms are:
* [Chandra-Toueg consensus algorithm](https://en.wikipedia.org/wiki/Chandra%E2%80%93Toueg_consensus_algorithm)
* [Paxos](https://en.wikipedia.org/wiki/Paxos_(computer_science))
* [Raft](https://en.wikipedia.org/wiki/Raft_(computer_science))

The consensus algorithm allows to choose exactly one leader node in the cluster.

.NEXT library provides protocol-agnostic implementation of Raft algorithm that can be adopted for any real network protocol. You can reuse this implementation which is located in [DotNext.Net.Cluster.Consensus.Raft](../../api/DotNext.Net.Cluster.Consensus.Raft.yml) namespace. If you want to know more about Raft then use the following links:
* [The Raft Consensus Algorithm](https://raft.github.io/)
* [The Secret Lives of Data](http://thesecretlivesofdata.com/)
* [In Search of an Understandable Consensus Algorithm](https://raft.github.io/raft.pdf)

# Replication
Replication allows to share information between nodes to ensure consistency between them. Usually, consensus algorithm covers replication process. In .NEXT library, replication functionality relies on the fact that each cluster node has its own persistent audit trail (or transaction log). However, the only default implementation of it is in-memory log which is suitable in siutations when your distributed application requires distributed consensus only and don't have distributed state that should be synchronized across cluster. If you need reliable replication then provide your own implementation of [IAuditTrail](../../api/DotNext.Net.Cluster.Replication.IAuditTrail-1.yml) interface.

[IReplicationCluster](../../api/DotNext.Net.Cluster.Replication.IReplicationCluster-1.yml) contains special method `WriteAsync` that allows clustered application to submit changes and wait unit they are replicated and committed. The behavior of this method depends on the passed [WriteConcern](../../api/DotNext.Net.Cluster.Replication.WriteConcern.yml) value. It regulates when to return control back to the client:
* _None_ indicates that control should be returned immediately after recording of changeset to the log. This mode doesn't provide any guarantees about consistency of the subsequent reads.
* _LeaderOnly_ indicates that control should be returned after commit by leader node. It means that leader has replicated the changes across cluster and mark its local changes as committed. In case of Raft this mode means that changes are replicated to the majority of nodes but committed only by leader.
* _Majority_ indicates that control should be returned to the caller when majority of nodes commit the changes.
* _All_ indicates that control should be returned to the caller when all clusted nodes commit the changes.

The current Raft implementation supports _None_ and _LeaderOnly_ write concerns only.

# Implementations
.NEXT offers extensions for ASP.NET Core which allow to build clustered microservices with the following features:
1. Messaging is fully supported and organized through HTTP protocol
1. Replication is fully supported
1. Consensus is fully supported and based on Raft algorithm

These extensions are located in [DotNext.Net.Cluster.Consensus.Raft.Http](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.yml) namespace. For more information, read [this article](./aspnetcore.md).
