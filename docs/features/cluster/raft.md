Raft
====
The core of Raft implementation is [RaftCluster&lt;TMember&gt;](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster-1.html) class which contains transport-agnostic implementation of Raft algorithm. First-class support of Raft in ASP.NET Core as well as other features are based on this class.

# Consensus
Correctness of consensus algorithm is tighly coupled with Write-Ahead Log defined via `AuditTrail` property of [IRaftCluster](../../api/DotNext.Net.Cluster.Consensus.Raft.IPersistentState.yml) interface or via Dependency Injection. If your application requires only consensus without replication of real data then [ConsensusOnlyState](../../api/DotNext.Net.Cluster.Consensus.Raft.ConsensusOnlyState.yml) implementation is used. Note that this implementation is used by default as well. It is lighweight and fast. However it doesn't store state on disk. Consider to use [persistent WAL](./wal.md) as fully-featured persistent log for Raft.

# State Recovery
The underlying state machine can be reconstruced at application startup using `InitializeAsync` method provided by implementation of [IPersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.IPersistentState.yml) interface. Usually, this method is called by .NEXT infrastructure automatically.

[PersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml) class exposes `ReplayAsync` method to do this manually. Read more about persistent Write-Ahead Log for Raft [here](./wal.md).

# Client Interaction
[Chapter 6](https://github.com/ongardie/dissertation/tree/master/clients) of Diego's dissertation about Raft contains recommendations about interaction between external client and cluster nodes. Raft implementation provided by .NEXT doesn't implement client session control as described in paper. However, it offers all necessary tools for that:
1. `IPersistentState.EnsureConsistencyAsync` waits until last committed entry is from leader's term
1. `RaftCluster.ForceReplicationAsync` initiates a new round of heartbeats and waits for reply from majority of nodes

Elimination of duplicate commands received from clients should be implemented manually because basic framework is not aware about underlying network transport.

# Network Transport
.NEXT supports the following network transports:
* HTTP 1.1 and HTTP 2.0 as a part of Clustered Microservices for ASP.NET Core
* TCP transport
* UDP transport

TCP and UDP network transports shipped with `DotNext.Net.Cluster` library without heavyweight dependencies such as ASP.NET Core or DotNetty. The library provides specialized [application protocol](https://en.wikipedia.org/wiki/Application_layer) on top of these transports which is binary protocol, highly optimized for Raft purposes and provide maximum bandwidth in contrast to HTTP. However, additional features for cluster programming are limited:
* General-purpose messaging between nodes is not supported via [IMessageBus](../../api/DotNext.Net.Cluster.Messaging.IMessageBus.yml) interface
* [IExpandableCluster](../../api/DotNext.Net.Cluster.IExpandableCluster.yml) interface is not implemented by default. However, the library consumer can implement it easily because entry point in the form of [RaftCluster](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster.html) class inherits from the core [RaftCluster&lt;TMember&gt;](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster-1.html) class.

Cluster programming model using TCP and UDP transports is unified and exposed via [RaftCluster](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster.html) class. The following example demonstrates usage of this class:
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
The configuration of the local node depends on chosen network transport. [NodeConfiguration](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster.NodeConfiguration.html) abstract class exposes common properties for both transports:

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| Metrics | No | **null** | Allows to specify custom metrics collector |
| PublicEndPoint | No | The same as `HostEndPoint` | Allows to specify real IP address of the host where cluster node launched. Usually it is needed when node executed inside of Docker container. If this parameter is not specified then cluster node may fail to detect itself because network interfaces inside of Docker container have different addresses in comparison with real host network interfaces |
| Partitioning | No | false | `true` if partitioning supported. In this case, each cluster partition may have its own leader, i.e. it is possible to have more that one leader for external observer. However, single partition cannot have more than 1 leader. `false` if partitioning is not supported and only one partition with majority of nodes can have leader. Note that cluster may be totally unavailable even if there are operating members presented |
| HeartbeatThreshold | No | 0.5 | Specifies frequency of heartbeat messages generated by leader node to inform follower nodes about its leadership. The range is (0, 1). The lower the value means that the messages are generated more frequently and vice versa |
| LowerElectionTimeout, UpperElectionTimeout | No | 150 | Defines range for election timeout (in milliseconds) which is picked randomly inside of it for each cluster member. If cluster node doesn't receive heartbeat from leader node during this timeout then it becomes a candidate and start a election. The recommended value for  _upperElectionTimeout_ is `2  X lowerElectionTimeout` |
| PipeConfig | No | [PipeOptions.Default](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipeoptions.default) | The configuration of I/O pipeline used for passing bytes between application and network transport |
| MemoryAllocator | No | Memory pool from _PipeConfig_ property | Memory allocator used to allocate memory for network packets |
| Metadata | No | Empty dictionary | A set of metadata properties associated with the local node |
| Members | Yes | Empty list | A set of cluster members. The list must contain address of the local node which is equal to _PublicEndPoint_ value |
| TimeToLive | No | 64 |  Time To Live (TTL) value of Internet Protocol (IP) packets |
| RequestTimeout | No | _UpperElectionTimeout_ | Defines request timeout for accessing cluster members across the network |
| LoggerFactory | No | [NullLoggerFactory.Instance](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging.abstractions.nullloggerfactory.instance) | The logger factory |


## ASP.NET Core
`DotNext.AspNetCore.Cluster` library is an extension for ASP.NET Core for writing microservices and supporting the following features:
1. Messaging is fully supported and organized through HTTP 1.1 or 2.0 protocol including TLS.
1. Replication is fully supported
1. Consensus is fully supported and based on Raft algorithm
1. Tight integration with ASP.NET Core ecosystem such as Dependency Injection and Configuration Object Model
1. Compatible with Kestrel or any other third-party web host
1. Detection of changes in the list of cluster nodes via configuration

These extensions are located in [DotNext.Net.Cluster.Consensus.Raft.Http](../../api/DotNext.Net.Cluster.Consensus.Raft.Http.yml) namespace. For more information, read [this article](./aspnetcore.md).

This implementation is WAN friendly because it uses reliable network transport and supports TLS. It is good choice if your cluster nodes communicate over Internet or any other unreliable network. However, HTTP leads to performance and traffic overhead. Moreover, the library depends on ASP.NET Core.

## TCP Transport
TCP transport used as bottom layer for specialized application protocol aimed to efficient transmission of Raft messages. This transport can be configured using [TcpConfiguration](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster.TcpConfiguration.html) class:
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
| GracefulShutdownTimeout | No | The same as _LowerElectionTimeout_ | The timeout of graceful shutdown of active incoming connections |
| TransmissionBlockSize | No | 65535 | The size, in bytes, of internal memory block used for sending packets. If your network has high packet loss then you can decrease this value to avoid retransmission of large blocks. |
| SslOptions | No | _N/A_ | Allows to enable and configure transport-level encryption using SSL and X.509 certificates |

TCP transport is WAN friendly and support transport-level encryption. However, the underlying application-level protocol is binary and can be a problem for corporate firewalls.

## UDP Transport
UDP transport used as bottom layer for specialized application protocol aimed to efficient transmission of Raft messages. This transport doesn't use persistent connection in contrast to TCP. As a result, it has no TCP overhead related to congestion and flow control of messages. These capabilities are implemented by application protocol itself. However, retransmission of lost packets is not implemented. The transport uses pessimistic approach and interprets lost packets as connection timeout. This is reasonable approach because the leader node examines other cluster members periodically and the next attempt may be successful. Some Raft messages such as **Vote** and **Heartbeat** with empty set of log entries (or if log entries are small enough) for replication can be easily placed to single datagram without fragmentation.

The transport has very low overhead which is equal to ~20 bytes per datagram. Therefore, most Raft messages can be placed to single datagram without streaming per request.

UDP transport cannot detect path [MTU](https://en.wikipedia.org/wiki/Maximum_transmission_unit) automatically and, by default, it uses minimal safe size of the datagram to avoid fragmentation. If need automatic path MTU discovery then use [MTU discovery](../core/mtu.md) mechanism from .NEXT. After that, you can specify datagram size using configuration properties.

This transport can be configured using [UdpConfiguration](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster.UdpConfiguration.html) class:

| Configuration parameter | Required | Default Value | Description |
| ---- | ---- | ---- | ---- |
| ServerBacklog | No | Equal to the number of cluster members | The number of incoming requests that can be handled simultaneously |
| ClientBacklog | No | Equal to the number of logical processor on the host machine | The number of outbound requests that can be initiated by the local node |
| DontFragment | No | true | Indicates that datagram cannot be fragmented by the underlying network layer such as IP (DF flag) |
| DatagramSize | No | 300 bytes | Represents UDP datagram size. For maximum performance, this property must be set to the maximum allowed transmission unit size by your network |
| LocalEndPoint | No | 0.0.0.0 with random port | Used for receiving responses from other cluster nodes |

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

## Example
There is Raft playground represented by RaftNode application. You can find this app [here](https://github.com/sakno/dotNext/tree/develop/src/examples/RaftNode). This playground allows to test Raft consensus protocol in real world using one of the supported transports: `http`, `tcp`, `tcp+ssl`, `udp`.

Each instance of launched application represents cluster node. All nodes can be started using the following script:
```bash
cd <dotnext>/src/examples/RaftNode
dotnet run -- tcp 3262
dotnet run -- tcp 3263
dotnet run -- tcp 3264
```

Every instance should be launched in separated Terminal session. After that, you will see diagnostics messages in `stdout` about election process. Press _Ctrl+C_ in the window related to the leader node and ensure that new leader will be elected.

Optionally, you can test replication powered by persistent WAL. To do that, you need to specify the name of folder which is used to store Write Ahead Log files
```bash
cd <dotnext>/src/examples/RaftNode
dotnet run -- tcp 3262 node1
dotnet run -- tcp 3263 node2
dotnet run -- tcp 3264 node3
```
Now you can see replication messages in each Terminal window. The replicated state stored in the `node1`, `node2` and `node3` folders. You can restart one of the nodes and make sure that its state is recovered correctly.

# Guide: How To Implement Database
This section contains recommendations about implementation of your own database or distributed service based on .NEXT Cluster programming model. It can be K/V database, distributed UUID generator, distributed lock or anything else.

1. Derive from [PersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml) class to implement core logic related to manipulation with state machine
    1. Override `ApplyAsync` method which contains interpretation of commands contained in log entries
    1. Override `CreateSnapshot` method which is responsible for log compaction
    1. Expose high-level data operations declared in the derived class in the form of interface. Let's assume that its name is `IDataEngine`
    1. Optionally override `FlushAsync` to handle notification from persistent log about the moment when the batch modification has completed
1. Declare class that is responsible for communication with leader node using custom messages
    1. This class aggregates reference to `IDataEngine`
    1. This class encapsulates logic for messaging with leader node
    1. This class acting as controller for API exposed to external clients
    1. Use `PersistentState.EnsureConsistencyAsync` to ensure that the node is fully synchronized with the leader node
    1. Use `PersistentState.WaitForCommitAsync` and, optionally, `RaftCluster.ForceReplicationAsync` methods for write operations
1. Expose data manipulation methods from class described above to clients using selected network transport
1. Implement duplicates elimination logic for write requests from clients
1. Call `ReplayAsync` method which is inherited from `PersistentState` class at application startup. This step is not need if you're using Raft implementation for ASP.NET Core.

`ForceReplicationAsync` method doesn't provide strong guarantees that the log entry at the specified index will be replicated and committed on return. A typical code for processing a new log entry from the client might be look like this:
```csharp
IRaftCluster cluster = ...;
var term = cluster.Term;
var index = await cluster.AuditTrail.AppendAsync(new MyLogEntry(term), token);
do
{
    await cluster.ForceReplicationAsync(TimeSpan.FromSeconds(30), token);
}
while (!await cluster.AuditTrail.WaitForCommitAsync(index, TimeSpan.FromSeconds(30), token));

// ensure that term wasn't changed
if (term != cluster.Term)
{
    // The term has changed, it means that the original log entry probably dropped or overwritten by newly elected
    // leader. As a result, original client request cannot be processed. You can ask the client to retry the request
    // or redirect the request to the leader node transparently
}
```

Designing binary format for custom log entries and interpreter for them may be hard. Examine [this](./wal.md) article to learn how to use Interpreter Framework shipped with the library.

# Guide: Custom Transport
Transport-agnostic implementation of Raft is represented by [RaftCluster&lt;TMember&gt;](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.RaftCluster-1.html) class. It contains core consensus and replication logic but it's not aware about network-specific details. You can use this class as foundation for your own Raft implementation for particular network protocol. All you need is to implementation protocol-specific communication logic.  This chapter will guide you through all necessary steps.

## Existing Implementations
.NEXT library ships multiple network transports: 
* [RaftHttpCluster](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.cs) as a part of `DotNext.AspNetCore.Cluster` library offers HTTP 1.1/HTTP 2 implementations adopted for ASP.NET Core framework. 
* [TransportServices](https://github.com/sakno/dotNext/tree/develop/src/cluster/DotNext.Net.Cluster/Net/Cluster/Consensus/Raft/TransportServices) as a part of `DotNext.Net.Cluster` library contains reusable network transport layer for UDP and TCP transport shipped as a part of this library.

All these implementations can be used as examples of transport for Raft messages.

## Architecture
`RaftCluster` contains implementation of consensus and replication logic so your focus is network-specific programming. First of all, you need to derive from this class. There are two main extensibility points when network-specific programing needed:
* `TMember` generic parameter which should be replaced with actual type argument by the derived class. Actual type argument should be a class implementing [IRaftClusterMember](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember.html) interface and other generic constraints. This part of implementation contains code necessary for sending Raft-specific messages over the wire.
* Body of derived class itself. This part of implementation contains code necessary for receiving Raft-specific messages over the wire.

From architecture point of view, these two parts are separated. However, the actual implementation may require a bridge between them.

## Cluster Member
[IRaftClusterMember](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember.html) declares the methods that are equivalent to Raft-specific message types.

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

`VoteAsync`, `PreVoteAsync`, `AppendEntriesAsync`, `InstallSnapshotAsync` are methods for sending Raft-specific messages over the wire. They are called automatically by core logic located in `RaftCluster` class. Implementation of these methods should throw [MemberUnavailableException](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.MemberUnavailableException.html) if any network-related problem occurred.

The last two methods responsible for serializing log entries to the underlying network connection. [IRaftLogEntry](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry.html) is inherited from [IDataTransferObject](https://sakno.github.io/dotNext/api/DotNext.IO.IDataTransferObject.html) which represents abstraction for Data Transfer Object. DTO is an object that can be serialized to or deserialized from binary form. However, serialization/deserialization process and binary layout are fully controlled by DTO itself in contrast to classic .NET serialization. You need to wrap underlying network stream to [IAsyncBinaryWriter](https://sakno.github.io/dotNext/api/DotNext.IO.IAsyncBinaryWriter.html) and pass it to `IDataTransferObject.WriteAsync` method for each log entry. `IAsyncBinaryWriter` interface has built-in static factory methods for wrapping [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter). Note that `IDataTransferObject.Length` may return **null** and you will not be able to identify log record size (in bytes) during serialization. This behavior depends on underlying implementation of Write-Ahead Log. You can examine value of `IAuditTrail.IsLogEntryLengthAlwaysPresented` property to apply necessary optimizations to the transmission process:
* If it's **true** then all log entries retrieved from such log has known size in bytes and `IDataTransferObject.Length` is not **null**. 
* If it's **false** then some or all log entries retrieved from such log has unknown size in bytes and `IDataTransferObject.Length` may be **null**. Thus, you need to provide special logic which allows to write binary data of undefined size to the underlying connection.

The default implementation for ASP.NET Core covers both cases. It uses multipart content type where log records separated by the special boundary from each other if `IAuditTrail.IsLogEntryLengthAlwaysPresented` returns **false**. Otherwise, more optimized transfer over the wire is applied. In this case, the overhead is comparable to the raw TCP connection.

`ResignAsync` method sends the message to the leader node and receiver should downgrade itself to the follower state. This is service message type not related to Raft but can be useful to force leader election.

You can use [this](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftClusterMember.cs) code as an example of HTTP-specific implementation.

## Derivation from RaftCluster
`RaftCluster` class contains all necessary methods for handling deserialized Raft messages:
* `ReceiveEntriesAsync` method allows to handle _AppendEntries_ Raft message type that was sent by another node
* `ReceiveResignAsync` method allows to handle leadership revocation procedure
* `ReceiveSnapshotAsync` method allows to handle _InstallSnapshot_ Raft message type that was sent by another node
* `ReceiveVoteAsync` method allows to handle _Vote_ Raft message type that was sent by another node
* `ReceivePreVoteAsync` method allows to handle _PreVote_ message introduced as extension to original Raft model to avoid inflation of _Term_ value

The underlying code responsible for listening network requests must restore Raft messages from transport-specific representation and call the necessary handler for particular message type. 

It is recommended to use **partial class** feature of C# language to separate different parts of the derived class. The recommended layout is:
* Main part with `StartAsync` and `StopAsync` methods containing initialization logic, configuration and other infrastructure-related aspects. The example is [here](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.cs)
* Raft-related messaging. The example is [here](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.Messaging.cs)
* General-purpose messaging (if you need it)

`ReceiveEntries` and `ReceiveSnapshot` expecting access to the log entries deserialized from the underlying transport. This is where `IDataTransformObject` concept comes again. `GetObjectData` method of this interface responsible for deserialization of DTO payload. Transport-specific implementation of `IRaftLogEntry` should be present on the receiver side. Everything you need is just wrap section of underlying stream into instance of [IAsyncBinaryReader](https://sakno.github.io/dotNext/api/DotNext.IO.IAsyncBinaryReader.html) and pass the reader to [Decoder](https://sakno.github.io/dotNext/api/DotNext.IO.IDataTransferObject.IDecoder-1.html) that comes through the parameter of `GetObjectData` method. The example is [here](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/AppendEntriesMessage.cs). `IAsyncBinaryReader` has static factory methods for wrapping [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipereader).

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

Another important aspect is a deduplication of Raft messages which is normal situation for TCP protocol. _Vote_ and _InstallSnapshot_ are idempotent messages and can be handled twice by receiver. However, _AppendEntries_ is not.

## Hosting Model
The shape of your API for transport-specific Raft implementation depends on how the potential users will host it. There are few possible situations:
* Using Dependency Injection container:
    * Generic application host from [Microsoft.Extensions.Hosting](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting) 
    * Web host from ASP.NET Core
    * Another Dependency Injection container
* Standalone application without DI container

In case of DI container from Microsoft you need to implement [IHostedService](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.hosting.ihostedservice) in your derived class. The signatures of `StartAsync` and `StopAsync` methods from `RaftCluster` class are fully compatible with this interface so you don't need implement interface methods manually. As a result, you will have automatic lifecycle management and configuration infrastructure at low cost. The instance of your class which is derived from `RaftCluster` should be registered as singleton service. All its interfaces should be registered separately. The example is [here](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpConfigurator.cs).

Different DI container requires correct adoption of your implementation.

If support of DI container is not a concern for you then appropriate configuration API and lifecycle management should be provided to the potential users.

The configuration of cluster member is represented by [IClusterMemberConfiguration](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IClusterMemberConfiguration.html) interface. Your configuration model should be based on this interface because it should be passed to the constructor of `RaftCluster` class. Concrete implementation of configuration model depends on the hosting model.

## Optional Features
By default, `RaftCluster` implements only [IReplicationCluster](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Replication.IReplicationCluster-1) interface. It means that transport-agnostic implementation supports basic cluster features such as leader election and replication. Cluster programming model in .NEXT offers optional cluster features:
* General-purpose messaging between members
* Dynamic addition/removal of members without restarting the whole cluster

If you want to support these features then appropriate interfaces must be implemented in your code. Learn more about these interfaces [here](./index.md).