Persistent Write Ahead Log
====
Starting with .NEXT 1.0.0 the library shipped with the general-purpose high-performance [persistent Write Ahead Log](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.yml)(WAL) providing the following features:
* Log compaction based on snapshotting
* File-based persistent storage for the log entries
* Caching
* Fast writes
* Parallel reads
* Automatic replays

However, it is not used as default audit trail by Raft implementation. You need to register it explicitly using Dependency Injection or configurator object.

Typically, `PersistentState` class is not used directly because it is not aware how to interpret commands contained in the log entries. This is the responsibility of the data state machine. It can be defined through overriding of the two methods:
1. `ValueTask ApplyAsync(LogEntry entry)` method is responsible for interpreting committed log entries and applying them to the underlying persistent data storage.
1. `SnapshotBuilder CreateSnapshotBuilder()` method is required if you want to enable log compaction. The returned builder squashes the series of log entries into the single log entry called **snapshot**. Then the snapshot can be persisted by the infrastructure automatically. By default, this method always returns **null** which means that compaction is not supported.

> [!NOTE]
> .NEXT library doesn't provide default implementation of the database or persistent data storage based on Raft replication

Internally, persistent WAL uses files to store the state of cluster member and log entries. The journal with log entries is not continuous. Each file represents the partition of the entire journal. Each partition is a chunk of sequential log entries. The maximum number of log entries per partition depends on the settings.

`PersistentState` has a rich set of tunable configuration parameters to achieve the best performance according with application needs:
* `recordsPerPartition` allows to define maximum number of log entries that can be stored continuously in the single partition. Log compaction algorithm depends on this value directly. When all records from the partition are committed and applied to the underlying state machine the infrastructure calls the snapshot builder and squashed all the entries in such partition. After that, write ahead log records the snapshot into the separated file and removes the partition file from the file system. The log compaction is expensive operation. So if you want to reduce the number of compactions then you need to increase the maximum number of log entries per partition. However, the partition file will take more disk space.
* `BufferSize` is the numbers of bytes that is allocated by persistent WAL in the memory to perform I/O operations. Set it to the maximum expected log entry size to achieve the best performance.
* `InitialPartitionSize` represents the initial pre-allocated size, in bytes, of the empty partition file. This parameter allows to avoid fragmentation of the partition file at file-system level.
* `UseCaching` is `bool` flag that allows to enable or disable in-memory caching of log entries metadata. `true` value allows to improve the performance or read/write operations by the cost of additional heap memory. `false` reduces the memory footprint by the cost of the read/write performance
* `UseSharedPool` specifies whether the [shared array pool](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1.shared?view=netstandard-2.1#System_Buffers_ArrayPool_1_Shared) should be used for internal purposes. If the parameter is `false` then the log will use dedicated array pool instead of shared pool.
* `MaxConcurrentReads` is a number of concurrent asynchronous operations which can perform reads in parallel. Write operations are always sequential. Ideally, the value should be equal to the number of nodes. However, the larger value consumes more system resources (e.g. file handles) and heap memory. 

Choose `recordsPerPartition` value with care because it cannot be changed for the existing persistent WAL.

Let's write a simple custom audit trail based on the `PersistentState` to demonstrate basics of Write Ahead Log. Our state machine stores only the single **long** value as the only possible persistent state.
```csharp
using DotNext.Net.Cluster.Consensus.Raft;
using System.Threading.Tasks;
using static System.Buffers.Binary.BinaryPrimitives;

sealed class SimpleAuditTrail : PersistentState
{
	internal long Value;	//the current int64 value synchronized across all cluster nodes

	//snapshot builder
	private sealed class SimpleSnapshotBuilder : SnapshotBuilder
	{
		private long currentValue;
		private readonly byte[] sharedBuffer;

		internal SimpleSnapshotBuilder(byte[] buffer) => sharedBuffer = buffer;

		//2.1
		public override Task CopyToAsync(Stream output, CancellationToken token)
		{
			WriteInt64LittleEndian(sharedBuffer, currentValue);
			return output.WriteAsync(sharedBuffer, 0, sizeof(long), token);
		}

		//2.2
		public override async ValueTask CopyToAsync(PipeWriter output, CancellationToken token)
		{
			WriteInt64LittleEndian(sharedBuffer, currentValue);
			await output.WriteAsync(new ReadOnlyMemory<byte>(sharedBuffer, 0, sizeof(long)), token);
		}

		//1
		protected override async ValueTask ApplyAsync(LogEntry entry) => currentValue = await Decode(entry);
	}
	
	//3
	private static async Task<long> Decode(LogEntry entry) => ReadInt64LittleEndian((await entry.ReadAsync(sizeof(long))).Span);
	
	//4
    protected override async ValueTask ApplyAsync(LogEntry entry) => Value = await Decode(entry);
	
	//5
    protected override SnapshotBuilder CreateSnapshotBuilder() => new SimpleSnapshotBuilder(Buffer);
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
* `EnsureConsistencyAsync` applies the committed entries to the underlying state machine or database engine. Usually, `CommitAsync` doing this automatically. This method can be called once at application startup to ensure that the database is up to date with latest committed changes
* `WaitForCommitAsync` waits for the specific commit

`ReadAsync` method can be used to obtain committed or uncommitted entries in stream-like manner.