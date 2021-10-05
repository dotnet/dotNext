Persistent Write Ahead Log
====
Starting with .NEXT 1.0.0 the library shipped with the general-purpose high-performance [persistent Write-Ahead Log](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState) providing the following features:
* Log compaction based on snapshotting
* File-based persistent storage for the log entries
* Caching
* Fast writes
* Parallel reads
* Automatic replays

However, it is not used as default audit trail by Raft implementation. You need to register it explicitly using Dependency Injection or configurator object.

Typically, `PersistentState` class is not used directly because it is not aware how to interpret commands contained in the log entries. This is the responsibility of the data state machine. It can be defined through overriding of the two methods:
1. `ValueTask ApplyAsync(LogEntry entry)` method is responsible for interpreting committed log entries and applying them to the underlying persistent data storage.
1. `SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext)` method is required if you want to enable log compaction. The returned builder squashes the series of log entries into the single log entry called **snapshot**. Then the snapshot can be persisted by the infrastructure automatically. By default, this method always returns **null** which means that compaction is not supported.

> [!NOTE]
> .NEXT library doesn't provide default implementation of the database or persistent data storage based on Raft replication

Internally, persistent WAL uses files to store the state of cluster member and log entries. The journal with log entries is not continuous. Each file represents the partition of the entire journal. Each partition is a chunk of sequential log entries. The maximum number of log entries per partition depends on the settings.

`PersistentState` has a rich set of tunable configuration parameters and overridable methods to achieve the best performance according with application needs:
* `recordsPerPartition` allows to define maximum number of log entries that can be stored continuously in the single partition. Log compaction algorithm depends on this value directly. When all records from the partition are committed and applied to the underlying state machine the infrastructure calls the snapshot builder and squashed all the entries in such partition. After that, the log writes the snapshot into the separated file and removes the partition files from the file system. Exact behavior of this procedure as well as its performance depends on chosen compaction mode.
* `BufferSize` is the number of bytes that is allocated by persistent WAL to perform I/O operations. Set it to the maximum expected log entry size to achieve the best performance.
* `SnapshotBufferSize` is the number of bytes that is allocated by persistent WAL to perform I/O operations related to log snapshot. By default it is equal to `BufferSize`. You can set it explicitly to the maximum expected size of the log entry to achieve the best performance.
* `InitialPartitionSize` represents the initial pre-allocated size, in bytes, of the empty partition file. This parameter allows to avoid fragmentation of the partition file at file-system level.
* `UseCaching` is `bool` flag that allows to enable or disable in-memory caching of log entries. `true` value allows to improve the performance or read/write operations by the cost of additional heap memory. `false` reduces the memory footprint by the cost of the read/write performance.
* `GetMemoryAllocator` generic method used for renting the memory and can be overridden
* `MaxConcurrentReads` is a number of concurrent asynchronous operations which can perform reads in parallel. Write operations are always sequential. Ideally, the value should be equal to the number of nodes. However, the larger value consumes more system resources (e.g. file handles) and heap memory.
* `ReplayOnInitialize` is a flag indicating that state of underlying database engine should be reconstructed when `InitializeAsync` is called by infrastructure. It can be done manually using `ReplayAsync` method.
* `WriteThrough` indicates that the WAL should write through any intermediate cache and go directly to disk. In other words, it calls _fsync_ for each written portion of data. By default this option is disabled that dramatically increases I/O performance. However, you may lost the data because OS has its own buffer for flushing written data, especially on SSD. You can enable reliable writes by the cost of the I/O performance.
* `BackupCompression` represents compression level used by `CreateBackupAsync` method.
* `CompactionMode` represents log compaction mode. The default is _Sequential_.
* `CopyOnReadOptions` allows to enable _copy-on-read_ behavior which allows to avoid lock contention between log compaction and replication processes
* `CacheEvictionPolicy` represents eviction policy of the cached log entries.

Choose `recordsPerPartition` value with care because it cannot be changed for the existing persistent WAL.

Let's write a simple custom audit trail based on the `PersistentState` to demonstrate basics of Write Ahead Log. Our state machine stores only the single **long** value as the only possible persistent state.

The example below additionally requires **DotNext.IO** library to simplify I/O work. 
```csharp
using DotNext.Buffers;
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

sealed class SimpleAuditTrail : PersistentState
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

		//2.1
		public override ValueTask CopyToAsync(Stream output, CancellationToken token)
		{
			return output.WriteAsync(currentValue, sharedBuffer.Memory, token);
		}

		//2.2
		public override async ValueTask CopyToAsync(PipeWriter output, CancellationToken token)
		{
			return output.WriteAsync(currentValue, token);
		}

		//1
		protected override async ValueTask ApplyAsync(LogEntry entry) => currentValue = await Decode(entry);

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

	//3
	private static async Task<long> Decode(LogEntry entry) => ReadInt64LittleEndian((await entry.ReadAsync(sizeof(long))).Span);

	//4
    protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await Decode(entry);
	
	//5
    protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context) => new SimpleSnapshotBuilder(context);
}
```
1)Aggregates the commited entry with the existing state; 2)called by infrastructure to serialize the aggregated state into stream; 3)Decodes the command from the log entry; 4) Applies the log entry to the state machine; 5)Creates snapshot builder

In the reality, the state machine should persist its state in reliable way, e.g. disk. The example above ignores this requirement for simplicity and maintain its state in the form of the field of type `long`.

# API Surface
`PersistentState` can be used as general purpose Write Ahead Log. Any changes in the cluster node state can be represented as a series for log entries that can be appended to the log. Newly added entries are not committed. It means that there is no confirmation from other cluster nodes about consistent state of the log. When the consistency is reached across cluster then the all appended entries marked as committed and the commands contained in the committed log entries can be applied to the underlying database engine.

The following methods allows to implement this scenario:
* `AppendAsync` adds a series of log entries to the log. All appended entries are in uncommitted state. Additionally, it can be used to replace entries with another entries
* `DropAsync` removes the uncommitted entries from the log
* `CommitAsync` marks appended entries as committed. Optionally, it can force log compaction
* `EnsureConsistencyAsync` suspends the caller and waits until the last committed entry is from leader's term
* `WaitForCommitAsync` waits for the specific or any commit
* `CreateBackupAsync` creates backup of the log packed into ZIP archive
* `ForceCompactionAsync` manually triggers log compaction. Has no effect if compaction mode other than _Background_

`ReadAsync` method can be used to obtain committed or uncommitted entries in stream-like manner.

# State Reconstruction
`PersistentState` is designed with assumption that underlying state machine can be reconstructed through sequential interpretation of each committed log entry stored in the log. When persistent WAL used in combination with other Raft infrastructure such as extensions for ASP.NET Core provided by **DotNext.AspNetCore.Cluster** library then this action performed automatically in host initialization code. However, if WAL used separately then reconstruction process should be initiated manually. To do that you need to call `ReplayAsync` method which reads all committed log entry and pass each entry to `ApplyAsync` protected method. Usually, `ApplyAsync` method implementation implements data state machine logic so sequential processing of all committed entries can restore its state correctly.

# Performance
Raft implementation must deal with the following bottlenecks: _network_, _disk I/O_ and synchronization when accessing WAL concurrently. The current implementation of persistent WAL provides many configuration options that allows to reduce the overhead caused by the last two aspects.

## Durable disk I/O
By default, `WriteThrough` option is disabled. It means that OS is responsible to decide when to do [fsync](https://man7.org/linux/man-pages/man2/fsync.2.html). The default behavior provides the best read/write throughput when accessing disk but may lead to corrupted WAL in case of server failures. All cached data in internal buffers will be lost. To increase durability you can set this property to **true** by the cost of I/O performance. However, this overhead can be mitigated by the caching mechanism.

## Caching
`UseCaching` allows to enable caching of the log entries and their metadata. If enabled, log entry metadata is always cached in the memory for all appended log entries by the cost of increased RAM consumption. Cached metadata allows to avoid disk I/O when requesting entries. However, reading of log entry payload still requires disk I/O. This overhead can be eliminated by the calling of _AppendAsync_ or _AppendAndEnsureCommitAsync_ method with **true** argument passed to **bool** parameter. In this case, the log entry will be copied to the memory outside of the internal lock and placed to the internal cache. Of course, write operation still requires persistence on the disk. However, any subsequent reads of the cached log entry doesn't require access to the disk.

The following operations have the positive impact provided by caching:
* All reads including replication process
* Commit process, because state machine interprets a series of log entries cached in the memory
* Snapshotting process, because snapshot builder deals with the log entries cached in the memory

## Lock contention
_Copy-on-read_ optimization allows to reduce lock contention between the compaction process that requires _compaction lock_ and readers. Multiple readers in parallel are allowed. Appending new log entries to the end of the log is also allowed in parallel with reading. However, log compaction is mutually exclusive with _read lock_. To avoid this contention, you can configure WAL using `CopyOnReadOptions` property. In this case, the replication process will be moved out of the lock. _Read lock_ still requires to create a range of log entries to be replicated.

## Log Compaction
WAL needs to squash old committed log entries to prevent growth of disk space usage. This procedure is called _log compaction_. As a result, the snapshot is produced. _Snapshot_ is a special kind of log entry that represents all squashed log entries up to the specified index calculated by WAL automatically. Log compaction is a heavy operation that may interfere with appending and reading operations due to lock contention.

`PersistentState` offers the following compaction modes:
* _Sequential_ offers the best optimization of disk space by the cost of read/write performance. During the sequential compaction, readers and appenders should wait until the end of the compaction procedure. In this case, wait time is proportional to the partition size (the number of entries per partition). Therefore, partition size should not be large. In this mode, the compaction algorithm trying to squash as much committed entries as possible during commit process. Even if you're committing the single entry, the compaction can be started for a entire partition. However, if the current partition is not full then compaction can be omitted.
* _Background_ doesn't block the appending of new entries. It means that appending of new entries can be done in parallel with the log compaction. Moreover, you can specify compaction factor explicitly. In this case, WAL itself is not responsible for starting the compaction. You need to do this manually using `ForceCompactionAsync` method. Disk space may grow unexpectedly if the rate of the appending of new entries is much higher than the speed of the compaction. However, this compaction mode allows to control the performance precisely. Read operations still blocked if there is an active compaction.
* _Foreground_ is forced automatically by WAL in parallel with the commit. In contrast to sequential compaction, the snapshot is created for the number of previously committed log entries equal to the number of currently committing entries. For instance, if you're trying to commit 4 log entries then the snapshot will be constructed for the last 4 log entries. In other words, this mode offers aggressive compaction forced on every commit but has low performance overhead. Moreover, the performance doesn't depend on partition size. This is the recommended compaction mode.

_Foreground_ and _Background_ compaction modes demonstrate the best I/O performance on SSD (because of parallel writes to the disk) while _Sequential_ is suitable for classic HDD.

## Snapshot Building
`CreateSnapshotBuilder(in SnapshotBuilderContext)` method of [PersistentState](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState) class allows to provide a snapshot builder which is responsible for snapshot construction. There are two types of builders:
* [Incremental builder](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState.IncrementalSnapshotBuilder) constructs the snapshot incrementally. The final snapshot is represented by the builder itself. The first applied log entry is always represented by the current snapshot. After the final log entry, the current snapshot will be rewritten completely by the constructed snapshot. In other words, the incremental builder has the following lifecycle:
	* Instantiate the builder
	* Apply the current snapshot
	* Apply other log entries
	* Build a new snapshot and rewrite the current snapshot with a new one
* [Inline builder](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState.InlineSnapshotBuilder) allows to apply the change to the existing snapshot on-the-fly. In means that there is no temporary representation of the snapshot during build process.

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
using DotNext.Net.Cluster.Consensus.Raft;

struct SubtractCommand
{
	public int X { get; set; }
	public int Y { get; set; }
}

PersistentState state = ...;
var entry = state.CreateJsonLogEntry(new SubtractCommand { X = 10, Y = 20 });
await state.AppendAsync(entry);
```

`SubtractCommand` must be JSON-serializable type. Its content will be serialialized to JSON and written as log entry. It's recommended to explicitly specify the optional parameters of `CreateJsonLogEntry` method to provide type identification independent from .NET type system.

Now the written log entry can be deserialized and interpreted easily inside of `AppendAsync` method:
```csharp
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;

sealed class SimpleAuditTrail : PersistentState
{
	internal long Value;
	
    protected override async ValueTask ApplyAsync(LogEntry entry)
	{
		switch (await entry.DeserializeFromJsonAsync())
		{
			case SubtractCommand command:
				Value = command.X - command.Y; // interpreting the command
				break;
		}
	}
}
```

`DeserializeFromJsonAsync` accepts type resolver as an optional argument. If default type resolution mechanism is used then persistent state stores type information in the form of fullt-qualified type name including assembly name.

## Command Interpreter
Interpreting of the custom log entries can be implemented with help of [Command Pattern](https://en.wikipedia.org/wiki/Command_pattern). [CommandInterpreter](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter) is a foundation for building custom interpreters in declarative way using such pattern. Each command has command handler described as separated method in the derived class.

First of all, we need to declare command types and write serialization/deserialization logic:
```csharp
using DotNext.Runtime.Serialization;
using DotNext.Net.Cluster.Consensus.Raft.Commands;
using System.Threading;
using System.Threading.Tasks;

struct SubtractCommand : ISerializable<SubtractCommand>
{
	public const int Id = 0;

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

struct NegateCommand : ISerializable<NegateCommand>
{
	public const int Id = 1;

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

Now the commands are described with their serialization logic. However, the interpreter still doesn't know how to interpet them. Let's derive from `CommandInterpreter` and write command handler for each command described above:
```csharp
using DotNext.Net.Cluster.Consensus.Raft.Commands;

[Command<SubtractCommand>(SubtractCommand.Id)]
[Command<NegateCommand>(NegateCommand.Id)]
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
Command types must be associated with theirs identifiers using [CommandAttribute&lt;TCommand&gt;](xref:DotNext.Net.Cluster.Consensus.Raft.Commands.CommandAttribute`1).

Each command handler must be decorated with `CommandHandlerAttribute` attribute and have the following signature:
* Return type is [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask)
* The first parameter is of command type
* The second parameter is [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)
* Must be public instance method

The handler of the log snapshot must be decored with `CommandHandlerAttribute` as well with `IsSnapshotHandler` property assigned to **true**:
```csharp
[CommandHandler(IsSnapshotHandler = true)]
public async ValueTask HandleSnapshotAsync(LogSnapshot command, CancellationToken token)
{
}
```
`LogSnapshot` here is a custom command describing a whole snapshot.

> [!NOTE]
> Snapshot command handler is applicable only if you're using [incremental snapshot builder](xref:DotNext.Net.Cluster.Consensus.Raft.PersistentState.IncrementalSnapshotBuilder).

`CommandInterpreter` automatically discovers all declared command handlers and associated command types. Now the log entry can be appended easily:
```csharp
MyInterpreter interpreter = ...;
PersistentState state = ...;
var entry = interpreter.CreateLogEntry(new SubtractCommand { X = 10, Y = 20 }, state.Term);
await state.AppendAsync(entry);
```

The last step is to combine the class derived from `PersistentState` and the custom interpreter.
```csharp
using DotNext.IO;
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;

sealed class SimpleAuditTrail : PersistentState
{
	private readonly MyInterpreter interpreter;
	
    protected override async ValueTask ApplyAsync(LogEntry entry)
	{
		await interpreter.InterpretAsync(entry);
	}
}
```

`InterpretAsync` is a method declared in base class `CommandInterpreter`. It decods the command identifier and delegates interpretation to the appropriate command handler.

Additionally, `CommandInterpreter` can be constructed without inheritance using Builder pattern:
```csharp
ValueTask SubtractAsync(SubtractCommand command, CancellationToken token)
{
	// interpretation logic
}

var interpreter = new CommandInterpreter.Builder()
	.Add<SubtractCommand>(SubtractCommand.Id, SubtractAsync)
	.Build();
```

Builder style can be used when you don't want to derive state machine engine from `CommandInterpreter` class for some reason.