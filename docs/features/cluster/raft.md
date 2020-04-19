Raft
====
This article describes details of Raft implementation provided by .NEXT library.

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

# Guide: How To Implement Database
This section contains recommendations about implementation of your own database or distributed service based on .NEXT Cluster programming model. It can be K/V database, distributed UUID generator, distributed lock or anything else.

1. Derive from [PersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml) class to implement core logic related to manipulation with state machine
    1. Override `ApplyAsync` method which contains interpretation of commands contained in log entries
    1. Override `CreateSnapshot` method which is responsible for log compaction
    1. Expose high-level data operations declared in the derived class in the form of interface. Let's assume that its name is `IDataEngine`
    1. Optionally override `FlushAsync` to handle notification from persistent log about the moment when batch modification completed
1. Declare class that is responsible for communication with leader node using custom messages
    1. This class aggregates reference to `IDataEngine`
    1. This class encapsulates logic for messaging with leader node
    1. This class acting as controller for API exposed to external clients
    1. Utilize `PersistentState.EnsureConsistencyAsync` and `RaftCluster.ForceReplicationAsync` methods for read-only operations
    1. Utilize `PersistentState.WaitForCommitAsync` for write operations
1. Expose data manipulation methods from class described above to clients using selected network transport
1. Implement duplicates elimination logic for write requests from clients
1. Call `ReplayAsync` method which is inherited from `PersistentState` class at application startup. This step is not need if you're using Raft implementation for ASP.NET Core.

# Guide: Custom Transport
Transport-agnostic implementation of Raft is represented by [RaftCluster](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.Net.Cluster/Net/Cluster/Consensus/Raft/RaftCluster.cs) class. It contains core consensus and replication logic but it's not aware about network-specific details. You can use this class as foundation for your own Raft implementation for particular network protocol. All you need is to implementation protocol-specific communication logic.  This chapter will guide you through all necessary steps.

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

`VoteAsync`, `AppendEntriesAsync`, `InstallSnapshotAsync` are methods for sending Raft-specific messages over the wire. They are called automatically by core logic encapsulated by `RaftCluster` class. Implementation of these methods should throw [MemberUnavailableException](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.MemberUnavailableException.html) if any network-related problem occurred.

The last two methods responsible for serializing log entries to the underlying network connection. [IRaftLogEntry](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftLogEntry.html) is inherited from [IDataTransferObject](https://sakno.github.io/dotNext/api/DotNext.IO.IDataTransferObject.html) which represents abstraction for Data Transfer Object. DTO is an object that can be serialized to or deserialized from binary form. However, serialization/deserialization process and binary layout are fully controlled by DTO itself in contrast to classic .NET serialization. You need to wrap underlying network stream to [IAsyncBinaryWriter](https://sakno.github.io/dotNext/api/DotNext.IO.IAsyncBinaryWriter.html) and pass it to `IDataTransferObject.WriteAsync` method for each log entry. `IAsyncBinaryWriter` interface has built-in static factory methods for wrapping [streams](https://docs.microsoft.com/en-us/dotnet/api/system.io.stream) and [pipes](https://docs.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter). Note that `IDataTransferObject.Length` may return **null** and you will not be able to identify log record size (in bytes) during serialization. This behavior depends on underlying implementation of Write-Ahead Log. Therefore, you need to provide special logic which allows to write binary data of undefined size to the underlying connection. For instance, default implementation for ASP.NET Core uses multipart content type where log records are separated by special boundary from each other so the knowledge about the size of each record is not needed.

`ResignAsync` method sends the message to the leader node and receiver should downgrade itself to the regular node. This is service message type not related to Raft but can be useful to force leader election.

You can use [this](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftClusterMember.cs) code as an example of HTTP-specific implementation.

## Derivation from RaftCluster
`RaftCluster` class contains all necessary methods for handling deserialized Raft messages:
* `ReceiveEntries` method allows to handle _AppendEntries_ Raft message type that was sent by another node
* `ReceiveResign` method allows to handle leadership revocation procedure
* `ReceiveSnapshot` method allows to handle _InstallSnapshot_ Raft message type that was sent by another node
* `ReceiveVote` method allows to handle _Vote_ Raft message type that was sent by another node

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
* Various [I/O enhancements](../core/io.md) aimed to simplify programming using pipes

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


