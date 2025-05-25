Persistent Write Ahead Log
====
.NEXT Cluster Programming Suite ships general-purpose high-performance [persistent Write-Ahead Log](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) with the following features:
* Log compaction based on snapshotting
* File-based persistent storage for the log entries
* Caching
* Fast writes in parallel with compaction
* Parallel reads
* Automatic replays

However, it is not used as default audit trail by Raft implementation. You need to register it explicitly.

Typically, `MemoryBasedStateMachine` class is not used directly because it is not aware how to interpret commands contained in the log entries. This is the responsibility of the data state machine. It can be defined through overriding of the two methods:
1. `ValueTask ApplyAsync(LogEntry entry)` method is responsible for interpreting committed log entries and applying them to the underlying persistent data storage.
1. `SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext)` method is required if you want to enable log compaction. The returned builder squashes the series of log entries into the single log entry called **snapshot**. Then the snapshot can be persisted by the infrastructure automatically. By default, this method always returns **null** which means that compaction is not supported.

> [!NOTE]
> .NEXT library doesn't provide default implementation of the database or persistent data storage based on Raft replication

Internally, persistent WAL uses files to store the state of cluster member and log entries. The journal with log entries is not continuous. Each file represents the partition of the entire journal. Each partition is a chunk of sequential log entries. The maximum number of log entries per partition depends on the settings.

`MemoryBasedStateMachine` has a rich set of tunable configuration parameters and overridable methods to achieve the best performance according with application needs:
* `recordsPerPartition` allows to define maximum number of log entries that can be stored continuously in the single partition. Log compaction algorithm depends on this value directly. When all records from the partition are committed and applied to the underlying state machine the infrastructure calls the snapshot builder and squashed all the entries in such partition. After that, the log writes the snapshot into the separated file and removes the partition files from the file system. Exact behavior of this procedure as well as its performance depends on chosen compaction mode.
* `BufferSize` is the number of bytes that is allocated by persistent WAL to perform I/O operations. Set it to the maximum expected log entry size to achieve the best performance.
* `SnapshotBufferSize` is the number of bytes that is allocated by persistent WAL to perform I/O operations related to log snapshot. By default it is equal to `BufferSize`. You can set it explicitly to the maximum expected size of the log entry to achieve the best performance.
* `InitialPartitionSize` represents the initial pre-allocated size, in bytes, of the empty partition file. This parameter allows to avoid fragmentation of the partition file at file-system level.
* `UseCaching` is `bool` flag that allows to enable or disable in-memory caching of log entries. `true` value allows to improve the performance or read/write operations by the cost of additional heap memory. `false` reduces the memory footprint by the cost of the read/write performance.
* `GetMemoryAllocator` generic method used for renting the memory and can be overridden
* `MaxConcurrentReads` is a number of concurrent asynchronous operations which can perform reads in parallel. Write operations are always sequential. Ideally, the value should be equal to the number of nodes. However, the larger value consumes more system resources (e.g. file handles) and heap memory.
* `ReplayOnInitialize` is a flag indicating that state of underlying database engine should be reconstructed when `InitializeAsync` is called by infrastructure. It can be done manually using `ReplayAsync` method.
* `WriteMode` indicates how WAL must manage intermediate buffers when performing disk I/O
* `IntegrityCheck` allows to verify internal WAL at initialization phase to ensure that the log was not damaged by hard shutdown
* `ParallelIO` indicates that the underlying storage device can perform read/write operations simultaneously. This parameter makes no sense if `UseCaching` is **true**. Otherwise, this option can be enabled only if the underlying storage is attached using parallel interface such as NVMe (via PCIe bus)
* `BackupCompression` represents compression level used by `CreateBackupAsync` method.
* `CompactionMode` represents log compaction mode. The default is _Sequential_
* `CopyOnReadOptions` allows to enable _copy-on-read_ behavior which allows to avoid lock contention between log compaction and replication processes
* `CacheEvictionPolicy` represents eviction policy of the cached log entries.

Choose `recordsPerPartition` value with care because it cannot be changed for the existing persistent WAL.

Let's write a simple custom audit trail based on the `MemoryBasedStateMachine` to demonstrate basics of Write Ahead Log. Our state machine stores only the single **long** value as the only possible persistent state.

The example below additionally requires **DotNext.IO** library to simplify I/O work.
```csharp
using DotNext.Buffers;
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

sealed class SimpleAuditTrail : MemoryBasedStateMachine
{
	internal long Value;	//the current int64 value synchronized across all cluster nodes

	//snapshot builder
	private sealed class SimpleSnapshotBuilder : IncrementalSnapshotBuilder
	{
		private long currentValue;
		private MemoryOwner<byte> sharedBuffer;

		internal SimpleSnapshotBuilder(in SnapshotBuilderContext context)
			: base(context)
		{
			sharedBuffer = context.Allocator.Invoke(2048, false);
		}

		// 1
		protected override async ValueTask ApplyAsync(LogEntry entry) => currentValue = await Decode(entry);

		// 2
		public override ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
			=> writer.WriteInt64Async(currentValue, true, token);

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				sharedBuffer.Dispose();
				sharedBuffer = default;
			}

			base.Dispose(disposing);
		}
	}

	public SimpleAuditTrail(Options options)
		: base(options)
	{
	}

	// 3
	private static ValueTask<long> Decode(LogEntry entry) => entry.ToTypeAsync<long, LogEntry>();

	// 4
    protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await Decode(entry);
	
	// 5
    protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context) => new SimpleSnapshotBuilder(context);
}
```
1)Aggregates the commited entry with the existing state; 2)called by infrastructure to serialize the aggregated state into stream; 3)Decodes the command from the log entry; 4) Applies the log entry to the state machine; 5)Creates snapshot builder

# API Surface
`MemoryBasedStateMachine` can be used as general purpose Write Ahead Log. Any changes in the cluster node state can be represented as a series for log entries that can be appended to the log. Newly added entries are not committed. It means that there is no confirmation from other cluster nodes about consistent state of the log. When the consistency is reached across cluster then the all appended entries marked as committed and the commands contained in the committed log entries can be applied to the underlying database engine.

The following methods allows to implement this scenario:
* `AppendAsync` adds a series of log entries to the log. All appended entries are in uncommitted state. Additionally, it can be used to replace entries with another entries
* `DropAsync` removes the uncommitted entries from the log
* `CommitAsync` marks appended entries as committed. Optionally, it can force log compaction
* `WaitForCommitAsync` waits for the specific or any commit
* `CreateBackupAsync` creates backup of the log packed into ZIP archive
* `ForceCompactionAsync` manually triggers log compaction. Has no effect if compaction mode other than _Background_

`ReadAsync` method can be used to obtain committed or uncommitted entries in stream-like manner.

# State Reconstruction
`MemoryBasedStateMachine` is designed with assumption that underlying state machine can be reconstructed through sequential interpretation of each committed log entry stored in the log. When persistent WAL used in combination with other Raft infrastructure such as extensions for ASP.NET Core provided by **DotNext.AspNetCore.Cluster** library then this action performed automatically in host initialization code. However, if WAL used separately then reconstruction process should be initiated manually. To do that you need to call `ReplayAsync` method which reads all committed log entry and pass each entry to `ApplyAsync` protected method. Usually, `ApplyAsync` method implementation implements data state machine logic so sequential processing of all committed entries can restore its state correctly.

# Performance
Raft implementation must deal with the following bottlenecks: _network_, _disk I/O_ and synchronization when accessing WAL concurrently. The current implementation of persistent WAL provides many configuration options that allows to reduce the overhead caused by the last two aspects.

## Durable disk I/O
By default, `WriteMode` option is `NoFlush`. It means that OS is responsible to decide when to do [fsync](https://man7.org/linux/man-pages/man2/fsync.2.html). The default behavior provides the best read/write throughput when accessing disk but may lead to corrupted WAL in case of server failures. All cached data in internal buffers will be lost. To increase durability you can set this property to `AutoFlush` or even `WriteThrough` by the cost of I/O performance.

## Caching
`UseCaching` allows to enable caching of the log entries and their metadata. In this case, the log entry will be copied to the memory outside of the internal lock and placed to the internal cache. Of course, write operation still requires persistence on the disk. However, any subsequent reads of the cached log entry doesn't require access to the disk.

Caching has a positive effect on the following operations:
* All reads including replication process
* Commit process, because state machine interprets a series of log entries cached in the memory
* Snapshotting process, because snapshot builder deals with the log entries cached in the memory

## Lock contention
_Copy-on-read_ optimization allows to reduce lock contention between the compaction process that requires _compaction lock_ and readers. Multiple readers in parallel are allowed. Appending new log entries to the end of the log is also allowed in parallel with reading. However, log compaction is mutually exclusive with _read lock_. To avoid this contention, you can configure WAL using `CopyOnReadOptions` property. In this case, the replication process will be moved out of the lock. _Read lock_ still requires to create a range of log entries to be replicated.

## Log Compaction
WAL needs to squash old committed log entries to prevent growth of disk space usage. This procedure is called _log compaction_. As a result, the snapshot is produced. _Snapshot_ is a special kind of log entry that represents all squashed log entries up to the specified index calculated by WAL automatically. Log compaction is a heavy operation that may interfere with appending and reading operations due to lock contention.

`MemoryBasedStateMachine` offers the following compaction modes:
* _Sequential_ offers the best optimization of disk space by the cost of read/write performance. During the sequential compaction, readers and appenders should wait until the end of the compaction procedure. In this case, wait time is proportional to the partition size (the number of entries per partition). Therefore, partition size should not be large. In this mode, the compaction algorithm trying to squash as much committed entries as possible during commit process. Even if you're committing the single entry, the compaction can be started for a entire partition. However, if the current partition is not full then compaction can be omitted.
* _Background_ doesn't block the appending of new entries. It means that appending of new entries can be done in parallel with the log compaction. Moreover, you can specify compaction factor explicitly. In this case, WAL itself is not responsible for starting the compaction. You need to do this manually using `ForceCompactionAsync` method. Disk space may grow unexpectedly if the rate of the appending of new entries is much higher than the speed of the compaction. However, this compaction mode allows to control the performance precisely. Read operations still blocked if there is an active compaction.
* _Foreground_ is forced automatically by WAL in parallel with the commit. In contrast to sequential compaction, the snapshot is created for the number of previously committed log entries equal to the number of currently committing entries. For instance, if you're trying to commit 4 log entries then the snapshot will be constructed for the last 4 log entries. In other words, this mode offers aggressive compaction forced on every commit but has low performance overhead. Moreover, the performance doesn't depend on partition size. This is the recommended compaction mode.
* _Incremental_ is a mix of _Foreground_ and _Sequential_ compaction modes. The snapshot is permanently located in the memory during entire WAL lifetime. A new log entry is applied to the underlying state machine and the snapshot builder on each commit. When the time for compaction comes, WAL just saves the snapshot to the disk from the memory.

_Foreground_ and _Background_ compaction modes demonstrate the best I/O performance on SSD (because of parallel writes to the disk) while _Sequential_ is suitable for classic HDD. _Incremental_ demonstrates the low overhead caused by log compaction but requires enough memory to keep the snapshot.

## Snapshot Building
`CreateSnapshotBuilder(in SnapshotBuilderContext)` method of [MemoryBasedStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) class allows to provide a snapshot builder which is responsible for snapshot construction. There are two types of builders:
* [Incremental builder](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine.IncrementalSnapshotBuilder) constructs the snapshot incrementally. The final snapshot is represented by the builder itself. The first applied log entry is always represented by the current snapshot. After the final log entry, the current snapshot will be rewritten completely by the constructed snapshot. In other words, the incremental builder has the following lifecycle:
    * Instantiate the builder
    * Apply the current snapshot
    * Apply other log entries
    * Build a new snapshot and rewrite the current snapshot with a new one
* [Inline builder](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine.InlineSnapshotBuilder) allows to apply the change to the existing snapshot on-the-fly. In means that there is no temporary representation of the snapshot during build process.

> [!NOTE]
> _Incremental_ snapshot builder and _Incremental_ log compaction are different things. You can choose any type of snapshot builder for _Incremental_ log compaction. However, the lifetime for the snapshot for _Incremental_ compaction differs from other compaction modes: it is created once per lifetime of Write-Ahead Log.

_Incremental_ approach is much simplier for programming. However, it is suitable for relatively simple WALs where the data in the log is of kilobytes or megabytes in size, because a whole snapshot must be interpreted at the start of build process.

_Inline_ approach doesn't require interpretation of a whole snapshot. Instead, you can modify the current snapshot according to the data associated with the log entry included in the scope of log compaction.

# Interpreter Framework
`ApplyAsync` method and snapshot builder responsible for interpretation of custom log entries usually containing the commands and applying these commands to the underlying database engine. [LogEntry](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState.LogEntry) is a generic representation of the log entry written to persistent WAL and it has no knowledge about semantics of the command. Therefore, you need to decode and interpret it manually.

There are two ways to do that:
1. JSON serialization
1. Deriving from [CommandInterpreter](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter) class

The first approach is very simple but may be not optimal for real application because each log entry must be represented as JSON in the form of UTF-8 encoded characters. Moreover, decoding procedure causes heap allocation of each decoded log entry.

## JSON log entries
At the time of appending of a new log entry, it can be created as follows:
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
using System.Threading.Tasks;

sealed class SimpleAuditTrail : MemoryBasedStateMachine
{
	internal long Value;
	
    protected override async ValueTask ApplyAsync(LogEntry entry)
	{
		var command = await JsonSerializable<TestJsonObject>.TransformAsync(entry);
		Value = command.X - command.Y; // interpreting the command
	}
}
```

## Command Interpreter
Interpreting of the custom log entries can be implemented with help of [Command Pattern](https://en.wikipedia.org/wiki/Command_pattern). [CommandInterpreter](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter) is a foundation for building custom interpreters in declarative way using such pattern. Each command has command handler described as separated method in the derived class.

First of all, we need to declare command types and write serialization/deserialization logic:
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

Each command must have unique identifier which is encoded transparently as a part of the log entry in WAL. This identifier is required by interpreter to correctly identify which serializer/deserializer must be called. Encoding of this identifier as a part of the custom serialization logic is not needed.

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
Command types must be associated with their identifiers using `Id` static property required by [ICommand&lt;T&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.ICommand`1) interface.

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

> [!NOTE]
> Snapshot command is applicable only if you're using [incremental snapshot builder](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine.IncrementalSnapshotBuilder).

`CommandInterpreter` automatically discovers all declared command handlers and associated command types.

The last step is to combine the class derived from `MemoryBasedStateMachine` and the custom interpreter.
```csharp
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;

sealed class SimpleAuditTrail : MemoryBasedStateMachine
{
	private readonly MyInterpreter interpreter;
	
    protected override async ValueTask ApplyAsync(LogEntry entry)
	{
		await interpreter.InterpretAsync(entry);
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

Builder style can be used when you don't want to derive state machine engine from `CommandInterpreter` class for some reason.

# Custom Write-Ahead Log
[Memory-based State Machine](xref:DotNext.Net.Cluster.Consensus.Raft.MemoryBasedStateMachine) provided out-of-the-box is not a _one size fits all_ solution. Its major drawback is log compaction performance: no new entries can be committed until snapshotting is done. For small state machines, incremental snapshot is an option. For better performance, it is recommended to use inline snapshotting that allows to modify the snapshot in-place without reinterpreting it. However, if state machine represents a huge volume of data even inline snapshotting will take significant amount of time. Background compaction provides granular control over how many committed log entry your want to compact. But write-heavy workload can produce log entries faster than background compaction can collect them.

Therefore, the existing implementation suitable for a limited set of scenarios. The best performance can be achieved with carefully selected underlying data structure according to the nature of the data, workload and the chosed database model: relational, K/V storage, document storage etc. Typically, memory-based state machine is suitable for implementing small K/V database, configuration storage or distributed lock.

[IPersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.IPersistentState) is an entry point to start an implementation of custom Write-Ahead Log from scratch. For performance reasons, you can combine implementation of WAL and the state machine in the same class.

In the same time, [Disk-based State Machine](xref:DotNext.Net.Cluster.Consensus.Raft.DiskBasedStateMachine) offers basic infrastructure for writing custom WAL. It keeps only recent changes in the memory. Snapshotting must be implemented by derived class.

## LSM Trees
[Log-structured merge tree](https://en.wikipedia.org/wiki/Log-structured_merge-tree) is a perfect data structure to implement write-heavy K/V database. Here you can find some tips about the architecture of custom WAL and state machine on top of LSM Trees. Most LSM trees used in practice employ multiple levels:
* **Level 0**. With help of [DiskBasedStateMachine](xref:DotNext.Net.Cluster.Consensus.Raft.DiskBasedStateMachine), you can organize the segments of log entries at this level. Each segment stores the log entries. When the log entry is committed, you can update in-memory state machine at this level with the committed entry. When all log entries in the segment are committed, you can persist in-memory state to the segment at Level 1. This approach allows to keep recent changes in the memory and easily recover in case of failure. When the state is persisted and moved to the Level 1, all committed entries can be discarded from the log.
* **Level 1**. At this level the implementation maintains the segments (or _runs_) persisted on the disk. New segments arrive from Level 0. The segment can be persisted using [SSTable](https://yetanotherdevblog.com/lsm/) format.
* **Level 2**. Growing number of segments at Level 1 may consume a lot of space on the disk. Most of segments may contain outdated data so they can be discarded. The actual implementation must merge multiple segments from Level 1 into a single segment. This process is called _compaction_. Compaction can be implemented as a background process. Here you need a global lock only for two things: delete merged segments and replace the existing snapshot with a new one.

The actual snapshot for replication is represented by a set of segments and the merged segment.

## B+ Trees
[B+ Tree](https://en.wikipedia.org/wiki/B%2B_tree) is another efficient data structure for storing structured data on the disk with fast access. In contrast to LSM tree, you don't need to keep in-memory sparse index for each segment.