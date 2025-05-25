Persistent Write Ahead Log
====
.NEXT Cluster Programming Suite ships general-purpose high-performance [persistent Write-Ahead Log](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.WriteAheadLog) with the following features:
* Log compaction based on snapshotting
* File-based persistent storage for the log entries
* Fast writes
* Parallel reads
* Automatic replays

> [!IMPORTANT]
> [WriteAheadLog](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.WriteAheadLog) is a long-term replacement of the [PersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState) class, which accepts only bug fixes. In the next major version of .NEXT, it will be removed. Its documentation is available [here](./old-wal.md).

Persistent WAL is not used as default audit trail by Raft implementation. You need to register it explicitly.

# Basics
Persistent WAL is the append-only structure that sits between the client and the state machine. When the client needs to modify the state, it needs to represent the modification as command in the form of the log entry and place that entry to the WAL. The log entry passes through the following states (in the order of transition):
1. **Appended** — the log entry is added to the WAL, but it's not yet replicated, and the consensus for it is not achieved. The effect of the log entry is not visible to the client;
2. **Committed** — the cluster achieves the consensus for that log entry, so it's replicated and can be executed to modify the state through state machine. The effect of the log entry is not visible to the client;
3. **Applied** — the log entry is executed by the state machine, so the state is modified. The effect of the log entry is visible to the client. At this point, the client can get the response that its modification request is processed successfully, and the node can send the ACK reply;
4. **Merged** — the log entry is a subject for removal from the WAL, because it's merged with the snapshot of the state, and the snapshot is saved to the disk.

The WAL uses memory-mapped files as a way to store the log entries. However, the WAL doesn't flush the mapped memory pages immediately on every append operation.

# State Machine
The state machine is represented by [IStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.IStateMachine) interface. The WAL requires its implementation.

Once the log entry is marked as **committed**, it can be executed to modify the underlying state. After that, the log entry is considered as **applied**. The process of applying committed log entries doesn't interfere with the process of committing of a new log entries. The Raft replication process is only interested in the committed log entries, while the client is interested in the applied log entries. According to the _linearizability_ property, the client must receive the ACK reply only when its request is turned into **applied** state. Thus, applying operation is always running in the background. This process is called _applier_. The process calls `ApplyAsync` method of the state machine.

Once the log entry is applied, the state reflects its effect. From time to time, the current state can be persisted in the form of the **snapshot**. When it happens, all the log entries up to the snapshot index can be marked as **merged**. It means that these log entries are no longer needed. They are not needed for any of the durability or other guarantees provided by the WAL. Thus, another background task called _cleaner_ can remove these log entries from the head of the WAL.

The state machine implementation produces snapshots incrementally. Each snapshot if one or more files on the disk. It's good to know when the outdated snapshot can be removed. The WAL invokes `ReclaimGarbageAsync` method to tell the state machine that it's the safe moment to remove outdated snapshots. The WAL considers the snapshot as outdated, when:
1. There is a snapshot that contains higher index of the **merged** log entry
1. There are no active readers of the outdated snapshot

Let's assume that we have two snapshots on the disk:
* Snapshot _A_ represents the state up to index 10
* Snapshot _B_ represents the state up to index 20

If the conditions for the outdated snapshot described above are met, `ReclaimGarbageAsync` implementation can safely remove the snapshot _A_. In other words, the method acts as a barrier.

## Simple state machine
[SimpleStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.SimpleStateMachine) is the base implementation of the state machine that maintains the entire state in the memory, but periodically creates a persistent snapshot. This type of the state machine is applicable for simple K/V or configuration databases. This type of the state machine requires the implementation of the following members:
* `RestoreAsync` — called by the infrastructure to restore in-memory state from the most up-to-date snapshot persisted on the disk;
* `PersistAsync` — called by the infrastructure to serialize the current in-memory state. Then, the state will be persisted to the disk as a snapshot;
* `ApplyAsync` — called by the infrastructure to apply the committed log entry to the current state. The method returns **bool** value indicating whether the snapshot must be created. Typically, the decision is based on the fact how fresh state will be on the disk in the case of failure recovery. Too infrequent snapshots can cause the node to request more up-to-date snapshot from another node in the cluster. It increases the recovery time of the node because the snapshot can be huge, and it needs to be transferred over the wire. Too frequent snapshots increase the number of the disk writes.

Reclamation of the outdated snapshots is implemented out of the box.

## Interpreter Framework
`ApplyAsync` method and snapshot builder responsible for interpretation of custom log entries usually containing the commands and applying these commands to the underlying database engine. [LogEntry](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.LogEntry) is the internal representation of the log entry maintained by the WAL, and it has no knowledge about semantics of the command. Therefore, you need to decode and interpret it manually.

There are two ways to do that:
1. JSON serialization
1. Deriving from [CommandInterpreter](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter) class

The first approach is a very simple but may be not optimal for real application because each log entry must be represented as JSON in the form of UTF-8 encoded characters. Moreover, decoding procedure causes heap allocation of each decoded log entry.

### JSON log entries
At the time of appending, it can be created as follows:
```csharp
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Text.Json;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SubtractCommand))]
public partial class SerializationContext : JsonSerializerContext
{
}

struct SubtractCommand : IJsonSerializable<SubtractCommand>
{
	public int X { get; set; }
	public int Y { get; set; }

	static JsonTypeInfo<TestJsonObject> IJsonSerializable<TestJsonObject>.TypeInfo => SerializationContext.Default.SubtractCommand;
}

MemoryBasedStateMachine state = ...;
var entry = state.CreateJsonLogEntry(new SubtractCommand { X = 10, Y = 20 });
await state.AppendAsync(entry);
```

`SubtractCommand` must be JSON-serializable type. Its content will be serialized to JSON and written as log entry. It's recommended to use [JSON polymorphic serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism).

Now the written log entry can be deserialized and interpreted easily inside of `AppendAsync` method:
```csharp
using DotNext.Text.Json;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading;
using System.Threading.Tasks;

sealed class MyStateMachine : SimpleStateMachine
{
	internal long Value;
	
    protected override async ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
	{
		var command = await JsonSerializable<TestJsonObject>.TransformAsync(entry, token);
		Value = command.X - command.Y; // interpreting the command
        return false;
	}
}
```

### Command Interpreter
Interpreting of the custom log entries can be implemented with the help of [Command Pattern](https://en.wikipedia.org/wiki/Command_pattern). [CommandInterpreter](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter) is a foundation for building custom interpreters in declarative way using such the pattern. Each command has command handler described as separated method in the derived class.

First, we need to declare command types and write serialization/deserialization logic:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Commands;
using System.Threading;
using System.Threading.Tasks;

struct SubtractCommand : ICommand<SubtractCommand>
{
	public static int Id => 0;

	public int X { get; set; }
	public int Y { get; set; }

	public async ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
		where TWriter : notnull, IAsyncBinaryWriter
	{
		await writer.WriteInt32Async(command.X, true, token);
		await writer.WriteInt32Async(command.Y, true, token);
	}

	public static async ValueTask<SubtractCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
		where TReader : notnull, IAsyncBinaryReader
	{
		return new SubtractCommand
		{
			X = await reader.ReadInt32Async(true, token),
			Y = await reader.ReadInt32Async(true, token)
		};
	}
}

struct NegateCommand : ICommand<NegateCommand>
{
	public static int Id => 1;

	public int X { get; set; }

	public async ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
		where TWriter : notnull, IAsyncBinaryWriter
	{
		await writer.WriteInt32Async(X, true, token);
	}

	public static async ValueTask<NegateCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
		where TReader : notnull, IAsyncBinaryReader
	{
		return new NegateCommand
		{
			X = await reader.ReadInt32Async(true, token)
		};
	}
}
```

Each command must have the unique identifier which is encoded transparently as a part of the log entry in WAL. Interpreter requires this identifier to correctly identify which serializer/deserializer must be called. Encoding of this identifier as a part of the custom serialization logic is not needed.

Now the commands are described with their serialization logic. However, the interpreter still doesn't know how to interpret them. Let's derive from `CommandInterpreter` and write command handler for each command described above:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Commands;

public class MyInterpreter : CommandInterpreter
{
	private long state;

	[CommandHandler]
	public async ValueTask SubtractAsync(SubtractCommand command, CancellationToken token)
	{
		state = command.X - command.Y;
	}

	[CommandHandler]
	public async ValueTask NegateAsync(NegateCommand command, CancellationToken token)
	{
		state = -command.X;
	}
}
```
Command types must be associated with their identifiers using `Id` static property required by [ICommand&lt;T&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.ICommand-1) interface.

Each command handler must be decorated with `CommandHandlerAttribute` attribute and have the following signature:
* Return type is [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask)
* The first parameter is of the command type (the type that implements `ICommand<T>` interface)
* The second parameter is [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)
* Must be a public instance method

The snapshot command must implement `IsSnapshot` static property:
```csharp
struct SnapshotCommand : ICommand<NegateCommand>
{
    public static bool IsSnapshot => true;
}
```

`CommandInterpreter` automatically discovers all declared command handlers and associated command types.

The last step is to combine the class derived from `SimpleStateMachine` and the custom interpreter.
```csharp
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;

sealed class SimpleAuditTrail : SimpleStateMachine
{
	private readonly MyInterpreter interpreter;
	
    protected override async ValueTask<bool> ApplyAsync(LogEntry entry, CancellationToken token)
	{
		await interpreter.InterpretAsync(entry, token);
        return false;
	}
}
```

`InterpretAsync` is a method declared in base class `CommandInterpreter`. It decodes the command identifier and delegates interpretation to the appropriate command handler.

Additionally, `CommandInterpreter` can be constructed without inheritance using the Builder pattern:
```csharp
ValueTask SubtractAsync(SubtractCommand command, CancellationToken token)
{
	// interpretation logic
}

var interpreter = new CommandInterpreter.Builder()
	.Add<SubtractCommand>(SubtractAsync)
	.Build();
```

## LSM Trees
[Log-structured merge tree](https://en.wikipedia.org/wiki/Log-structured_merge-tree) is a perfect data structure to implement the write-heavy K/V database. Here you can find some tips about the architecture of the state machine on top of LSM Trees. Most LSM trees used in practice employ multiple levels:
* **Level 0** sits in the memory and accepts a new updates through `IStateMachine.ApplyAsync` methods. When it's time to move the current in-memory state to the next LSM run, the implementer needs to return appropriate **bool** value;
* **Level 1**: represents LSM run persisted on the disk
* **Level N**. Growing number of segments at Level 1 may consume a lot of space on the disk. Most of the segments may contain outdated data so they can be discarded. The actual implementation must merge multiple segments from Level 1 into a single segment. This process is called _compaction_. Compaction can be implemented as a background process. `ReclaimGarbageAsync` method is called at the safe point in time when the outdated runs can be merged and removed from the disk;

Also, the implementation needs to keep the sparse index in the memory for fast navigation across runs.

## B+ Trees
[B+ Tree](https://en.wikipedia.org/wiki/B%2B_tree) is another efficient data structure for storing structured data on the disk with fast access. In contrast to the LSM trees, you don't need to keep in-memory sparse index for each segment.

# Durability
When the log entry is in **appended** state, it's written to the one or more memory pages mapped to the files. However, the WAL doesn't flush these memory pages immediately. It's safe to lose appended log entries in this state, because there is no consensus reached for that log entry, and it's not yet replicated to the majority of the node.

When the log entry is replicated to the majority of the cluster, it can be committed. Only committed log entries are subject to persistence. The background job, _flusher_, flushes the memory pages to the disk. However, the disk I/O does not block the commit process. Additionally, the operating system itself can flush mapped memory pages. It's still safe, because
1. In the case of system reboot or any other form of the process shutdown (even process crash due to unhandled exception), the mapped memory pages remain alive and maintained by the operating system. Eventually, the operating system flushes these pages to the disk
2. In the case of power outage or any other form of the system hard reset, the WAL keeps the checkpoint, which helps to recover the underlying state of the node. Moreover, since the log entry is replicated to the majority, the node can replicate it back even if it was lost in the committed state locally.

The _flusher_ automatically updates the _checkpoint_ when it gets the confirmation from the OS that memory pages up to the specified log entry index are written to the disk. In the case of failure, the WAL gets the checkpoint and asks the state machine to apply all the log entries, starting from the snapshot index up to the checkpoint.

As a result, _durability_ property is guaranteed by the liveness of the quorum.

# Configuration
The configuration of the WAL is represented by [Options](xref:DotNext.Net.Cluster.Consensus.Raft.StateMachine.WriteAheadLog.Options) class. It exposes the following configuration properties:
* `Location` represents the root directory to the WAL files. This property is required;
* `FlushInterval` describes how frequently the WAL must flush the memory pages to the disk to create a checkpoint
  * `0` means that the flush is triggered on every commit in the background. This is the best option for fast failure recovery, but produces a lot of I/O operations
  * `InfiniteTimeSpan` disables automatic flush and creation of the checkpoint. `FlushAsync` method must be called explicitly when needed
  * Any other positive value represents the time interval when the WAL produces the checkpoint
* `ChunkMaxSize` represents the size of the single chunk file. The WAL uses multiple files to store the log entries. When the cleanup process decides to remove the WAL head, it deletes the old files. Depending on the size of the log entries, every chunk file may contain several log entries. The size is always rounded to internal page size, which is 4K bytes. The size should be constant and cannot be changed once the WAL is created. If the log entry size is larger than the chunk size, it gets fragmented, which slows down the following read by the state machine
* `ConcurrencyLevel` represents the expected number of concurrent calls to the WAL. The main source of the concurrent is client requests
* `MeasurementTags` is a collection of tags to be attached to all metrics

# Performance
New appends go to the mapped memory pages directly, never to the disk. The amount of occupied memory depends on the frequency of the snapshots. Once the snapshot is created, all log entries prior it can be deallocated from the memory as well as deleted on the disk. Flushed memory pages are not marked as dirty, so the OS can unmap physical memory related to these pages.

In other words, the time of the transition **appended** => **applied** depends on two factors:
1. Network bandwidth and latency, because the leader needs to replicate the log entry to the majority of the cluster to achieve the consensus. This is required to transit the log entry from **applied** state to **committed** state, which is needed for further transition to **applied** state
1. Memory speed, which can be actually ignored, because it's an order of magnitude faster than the network

# Telemetry
The WAL exposes a group of metrics through `DotNext.IO.WriteAheadLog` meter. The metrics can be discovered with `dotnet-counters` tool from the .NET SDK.