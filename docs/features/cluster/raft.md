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

# How To Implement Database
This section contains recommendations about implementation of your own database based on .NEXT Cluster programming model. It can be K/V database or something else.

1. Derive from [PersistentState](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml) class to implement core logic related to manipulation with state machine
    1. Override `ApplyAsync` method which contains interpretation of commands contained in log entries
    1. Override `CreateSnapshot` method which is responsible for log compaction
    1. Expose high-level data operations declared in the derived class in the form of interface. Let's assume that its name is `IDataEngine`.
1. Declare class that is responsible for communication with leader node using custom messages
    1. This class aggregates reference to `IDataEngine`
    1. This class encapsulates logic for messaging with leader node
    1. This class acting as controller for API exposed to external clients
    1. Utilize `PersistentState.EnsureConsistencyAsync` and `RaftCluster.ForceReplicationAsync` methods for read-only operations
    1. Utilize `PersistentState.WaitForCommitAsync` for write operations
1. Now expose data manipulation methods from class described above to clients using selected network transport
1. Implement duplicates elimination logic for write requests from clients
1. Call `ReplayAsync` method which is inherited from `PersistentState` class at application startup

You can use distributed lock implementation provided by .NEXT as an example:
1. [DistributedLockProvider](../../api/DotNext.Net.Cluster.DistributedServices.DistributedLockProvider.yml) represents controller class
1. [DistributedApplicationState](../../api/DotNext.Net.Cluster.Consensus.Raft.DistributedApplicationState.yml) represents logic for interaction with persistent log.
