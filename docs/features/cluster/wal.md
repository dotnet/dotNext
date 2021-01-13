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

`PersistentState` has a rich set of tunable configuration parameters and overridable methods to achieve the best performance according with application needs:
* `recordsPerPartition` allows to define maximum number of log entries that can be stored continuously in the single partition. Log compaction algorithm depends on this value directly. When all records from the partition are committed and applied to the underlying state machine the infrastructure calls the snapshot builder and squashed all the entries in such partition. After that, write ahead log records the snapshot into the separated file and removes the partition file from the file system. The log compaction is expensive operation. So if you want to reduce the number of compactions then you need to increase the maximum number of log entries per partition. However, the partition file will take more disk space.
* `BufferSize` is the numbers of bytes that is allocated by persistent WAL in the memory to perform I/O operations. Set it to the maximum expected log entry size to achieve the best performance.
* `InitialPartitionSize` represents the initial pre-allocated size, in bytes, of the empty partition file. This parameter allows to avoid fragmentation of the partition file at file-system level.
* `UseCaching` is `bool` flag that allows to enable or disable in-memory caching of log entries metadata. `true` value allows to improve the performance or read/write operations by the cost of additional heap memory. `false` reduces the memory footprint by the cost of the read/write performance
* `GetMemoryAllocator` generic method used for renting the memory and can be overridden
* `MaxConcurrentReads` is a number of concurrent asynchronous operations which can perform reads in parallel. Write operations are always sequential. Ideally, the value should be equal to the number of nodes. However, the larger value consumes more system resources (e.g. file handles) and heap memory.
* `ReplayOnInitialize` is a flag indicating that state of underlying database engine should be reconstructed when `InitializeAsync` is called by infrastructure. It can be done manually using `ReplayAsync` method.

Choose `recordsPerPartition` value with care because it cannot be changed for the existing persistent WAL.

Let's write a simple custom audit trail based on the `PersistentState` to demonstrate basics of Write Ahead Log. Our state machine stores only the single **long** value as the only possible persistent state.

The example below additionally requires **DotNext.IO** library to simplify I/O work. 
```csharp
using DotNext.IO;
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
		private readonly Memory<byte> sharedBuffer;

		internal SimpleSnapshotBuilder(Memory<byte> buffer) => sharedBuffer = buffer;

		//2.1
		public override ValueTask CopyToAsync(Stream output, CancellationToken token)
		{
			return output.WriteAsync(currentValue, buffer, token);
		}

		//2.2
		public override async ValueTask CopyToAsync(PipeWriter output, CancellationToken token)
		{
			return output.WriteAsync(currentValue, token);
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
* `EnsureConsistencyAsync` suspends the caller and waits until the last committed entry is from leader's term
* `WaitForCommitAsync` waits for the specific or any commit

`ReadAsync` method can be used to obtain committed or uncommitted entries in stream-like manner.

# State Reconstruction
`PersistentState` is designed with assumption that underlying state machine can be reconstructed through sequential interpretation of each committed log entry stored in the log. When persistent WAL used in combination with other Raft infrastructure such as extensions for ASP.NET Core provided by **DotNext.AspNetCore.Cluster** library then this action performed automatically in host initialization code. However, if WAL used separately then reconstruction process should be initiated manually. To do that you need to call `ReplayAsync` method which reads all committed log entry and pass each entry to `ApplyAsync` protected method. Usually, `ApplyAsync` method implementation implements data state machine logic so sequential processing of all committed entries can restore its state correctly.

# Interpreter Framework
`ApplyAsync` method and snapshot builder responsible for interpretation of custom log entries usually containing the commands and applying these commands to the underlying database engine. [LogEntry](../../api/DotNext.Net.Cluster.Consensus.Raft.PersistentState.LogEntry.yml) is a generic representation of the log entry written to persistent WAL and it has no knowledge about semantics of the command. Therefore, you need to decode and interpret it manually.

There are two ways to do that:
1. JSON serialization
1. Deriving from [CommandInterpreter](../../api/DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter.yml) class

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
Interpreting of the custom log entries can be implemented with help of [Command Pattern](https://en.wikipedia.org/wiki/Command_pattern). [CommandInterpreter](../../api/DotNext.Net.Cluster.Consensus.Raft.Commands.CommandInterpreter.yml) is a foundation for building custom interpreters in declarative way using such pattern. Each command has command handler described as separated method in the derived class.

First of all, it's needed to decorate command type with necessary attribute and write serialization and deserialization logic:
```csharp
using DotNext.Runtime.Serialization;
using DotNext.Net.Cluster.Consensus.Raft.Commands;
using System.Threading;
using System.Threading.Tasks;

sealed class CommandFormatter : IFormatter<SubtractCommand>, IFormatter<NegateCommand>
{
	public static readonly CommandFormatter Instance = new CommandFormatter();

	private CommandFormatter()
	{
	}

    async ValueTask IFormatter<SubtractCommand>.SerializeAsync<TWriter>(SubtractCommand command, TWriter writer, CancellationToken token)
	{
		await writer.WriteInt32Async(command.X, true, token);
		await writer.WriteInt32Async(command.Y, true, token);
	}

	async ValueTask<SubtractCommand> IFormatter<SubtractCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
	{
		return new SubtractCommand
		{
			X = await reader.ReadInt32Async(true, token),
			Y = await reader.ReadInt32Async(true, token)
		};
	}

	async ValueTask IFormatter<NegateCommand>.SerializeAsync<TWriter>(NegateCommand command, TWriter writer, CancellationToken token)
	{
		await writer.WriteInt32Async(command.X, true, token);
	}

	async ValueTask<NegateCommand> IFormatter<NegateCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
	{
		return new NegateCommand
		{
			X = await reader.ReadInt32Async(true, token),
		};
	}
}

[CommandAttribute(0, Formatter = typeof(CommandFormatter), FormatterMember = nameof(CommandFormatter.Instance))]
struct SubtractCommand
{
	public int X { get; set; }
	public int Y { get; set; }
}

[CommandAttribute(1, Formatter = typeof(CommandFormatter), FormatterMember = nameof(CommandFormatter.Instance))]
struct NegateCommand
{
	public int X { get; set; }
}
```

Each command must have unique identifier which is encoded transparently as a part of the log entry in WAL. This identifier is required by interpreter to correctly identify which serializer/deserializer must be called. Encoding of this identifier as a part of the custom serialization logic is not needed.

Now the commands are described with their serialization logic. However, the interpreter still doesn't know how to interpet them. Let's derive from `CommandInterpreter` and write command handler for each command described above:
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
Each command handler must be decorated with `CommandHandlerAttribute` attribute and have the following signature:
* Return type is [ValueTask](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.valuetask)
* The first parameter is of command type
* The second parameter is [CancellationToken](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)
* Must be public instance method

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
	.Add<SubtractCommand>(SubtractAsync, CommandFormatter.Instance)
	.Build();
```

Builder style can be used when you don't want to derive state machine engine from `CommandInterpreter` class for some reason.