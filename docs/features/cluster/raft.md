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
This section contains recommendations about implementation of your own database based on .NEXT Cluster programming model. It can be K/V database or something else.

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
.NEXT library ships [RaftHttpCluster](https://github.com/sakno/dotNext/blob/master/src/cluster/DotNext.AspNetCore.Cluster/Net/Cluster/Consensus/Raft/Http/RaftHttpCluster.cs) as a part of `DotNext.AspNetCore.Cluster` library and offers HTTP 1.1/HTTP 2 implementations adopted for ASP.NET Core framework. It can be used as starting point to learn about protocol-specific implementation of Raft in addition to this chapter.

## Architecture
`RaftCluster` contains implementation of consensus and replication logic so your focus is network-specific programming. First of all, you need to derive from this class. There are two main extensibility points when network-specific programing needed:
* `TMember` generic parameter which should be replaced with actual type argument by the derived class. Actual type argument should be a class implementing [IRaftClusterMember](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember.html) interface and other generic constraints. This part of implementation contains code necessary for sending Raft-specific messages over the wire (hereinafter, **sender**).
* Body of derived class itself. This part of implementation contains code necessary for receiving Raft-specific messages over the wire (hereinafter, **receiver**).

From architecture point of view, these two parts are separated. However, the actual implementation requires a bridge between **sender** and **receiver** components

## Cluster Member
[IRaftClusterMember](https://sakno.github.io/dotNext/api/DotNext.Net.Cluster.Consensus.Raft.IRaftClusterMember.html) declares the methods repeating Raft-specific message types.  

## Hosting Model
Your application should be 

