using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using Runtime.Serialization;

public sealed class CommandInterpreterTests : Test
{
    private enum BinaryOperation
    {
        Add = 0,
        Subtract
    }

    private enum UnaryOperation
    {
        Negate = 0,
        OnesComplement
    }

    private struct BinaryOperationCommand : ISerializable<BinaryOperationCommand>
    {
        internal const int Id = 0;

        public int X, Y;
        public BinaryOperation Type;

        long? IDataTransferObject.Length => sizeof(int) + sizeof(int) + sizeof(BinaryOperation);

        async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            await writer.WriteLittleEndianAsync(X, token);
            await writer.WriteLittleEndianAsync(Y, token);
            await writer.WriteLittleEndianAsync((int)Type, token);
        }

        public static async ValueTask<BinaryOperationCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new BinaryOperationCommand
            {
                X = await reader.ReadLittleEndianAsync<int>(token),
                Y = await reader.ReadLittleEndianAsync<int>(token),
                Type = (BinaryOperation)await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct UnaryOperationCommand : ISerializable<UnaryOperationCommand>
    {
        internal const int Id = 1;

        public int X;
        public UnaryOperation Type;

        readonly long? IDataTransferObject.Length => sizeof(int) + sizeof(UnaryOperation);

        readonly async ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        {
            await writer.WriteLittleEndianAsync(X, token);
            await writer.WriteLittleEndianAsync((int)Type, token);
        }

        public static async ValueTask<UnaryOperationCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new UnaryOperationCommand
            {
                X = await reader.ReadLittleEndianAsync<int>(token),
                Type = (UnaryOperation)await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct AssignCommand : ISerializable<AssignCommand>
    {
        internal const int Id = 3;

        public int Value;

        readonly long? IDataTransferObject.Length => sizeof(int);

        readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteLittleEndianAsync(Value, token);

        public static async ValueTask<AssignCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new AssignCommand
            {
                Value = await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct SnapshotCommand : ISerializable<SnapshotCommand>
    {
        internal const int Id = 4;

        public int Value;

        readonly long? IDataTransferObject.Length => sizeof(int);

        readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteLittleEndianAsync(Value, token);

        public static async ValueTask<SnapshotCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new SnapshotCommand
            {
                Value = await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    [Command<BinaryOperationCommand>(BinaryOperationCommand.Id)]
    [Command<UnaryOperationCommand>(UnaryOperationCommand.Id)]
    [Command<SnapshotCommand>(SnapshotCommand.Id)]
    private sealed class CustomInterpreter : CommandInterpreter
    {
        internal int Value;

        internal static ValueTask DoBinaryOperation(ref int value, BinaryOperationCommand command, CancellationToken token)
        {
            value = command.Type switch
            {
                BinaryOperation.Add => command.X + command.Y,
                BinaryOperation.Subtract => command.X - command.Y,
                _ => throw new NotSupportedException()
            };

            return token.IsCancellationRequested ? new ValueTask(Task.FromCanceled(token)) : new ValueTask();
        }

        [CommandHandler]
        public ValueTask DoBinaryOperation(BinaryOperationCommand command, object context, CancellationToken token)
        {
            Null(context);
            return DoBinaryOperation(ref Value, command, token);
        }

        internal static ValueTask DoUnaryOperation(ref int value, UnaryOperationCommand command, CancellationToken token)
        {
            value = command.Type switch
            {
                UnaryOperation.Negate => -command.X,
                UnaryOperation.OnesComplement => ~command.X,
                _ => throw new NotSupportedException()
            };

            return token.IsCancellationRequested ? new ValueTask(Task.FromCanceled(token)) : new ValueTask();
        }

        [CommandHandler]
        public ValueTask DoUnaryOperation(UnaryOperationCommand command, CancellationToken token)
            => DoUnaryOperation(ref Value, command, token);

        [CommandHandler(IsSnapshotHandler = true)]
        public ValueTask ApplySnapshot(SnapshotCommand command, CancellationToken token)
        {
            Value = command.Value;
            return token.IsCancellationRequested ? new ValueTask(Task.FromCanceled(token)) : new ValueTask();
        }
    }

    private sealed class TestPersistenceState : MemoryBasedStateMachine
    {
        private readonly CustomInterpreter interpreter;

        public TestPersistenceState()
            : base(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), 4, new Options { CompactionMode = CompactionMode.Background })
        {
            interpreter = new CustomInterpreter();
        }

        internal int Value => interpreter.Value;

        internal LogEntry<TCommand> CreateLogEntry<TCommand>(TCommand command)
            where TCommand : struct, ISerializable<TCommand>
            => interpreter.CreateLogEntry(command, Term);

        protected override ValueTask ApplyAsync(LogEntry entry)
            => new(interpreter.InterpretAsync(entry).AsTask());

        protected override SnapshotBuilder CreateSnapshotBuilder(in SnapshotBuilderContext context)
            => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                interpreter.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override ValueTask DisposeAsyncCore()
        {
            interpreter.Dispose();
            return base.DisposeAsyncCore();
        }
    }

    [Fact]
    public static async Task MethodsAsHandlers()
    {
        using var interpreter = new CustomInterpreter();
        var entry1 = interpreter.CreateLogEntry(new BinaryOperationCommand { X = 40, Y = 2, Type = BinaryOperation.Add }, 1L);
        Equal(1L, entry1.Term);
        Equal(0, interpreter.Value);
        Equal(0, await interpreter.InterpretAsync(entry1));
        Equal(42, interpreter.Value);

        var entry2 = interpreter.CreateLogEntry(new UnaryOperationCommand { X = 42, Type = UnaryOperation.Negate }, 10L);
        Equal(10L, entry2.Term);
        Equal(1, await interpreter.InterpretAsync(entry2));
        Equal(-42, interpreter.Value);
    }

    [Fact]
    public static async Task DelegatesAsHandlers()
    {
        var state = new StrongBox<int>();

        var interpreter = new CommandInterpreter.Builder()
            .Add(BinaryOperationCommand.Id, new Func<BinaryOperationCommand, CancellationToken, ValueTask>(BinaryOp))
            .Add(UnaryOperationCommand.Id, new Func<UnaryOperationCommand, CancellationToken, ValueTask>(UnaryOp))
            .Add(AssignCommand.Id, new Func<AssignCommand, object, CancellationToken, ValueTask>(AssignOp))
            .Build();

        var entry1 = interpreter.CreateLogEntry(new BinaryOperationCommand { X = 40, Y = 2, Type = BinaryOperation.Add }, 1L);
        Equal(1L, entry1.Term);
        Equal(0, state.Value);
        Equal(0, await interpreter.InterpretAsync(entry1));
        Equal(42, state.Value);

        var entry2 = interpreter.CreateLogEntry(new UnaryOperationCommand { X = 42, Type = UnaryOperation.Negate }, 10L);
        Equal(10L, entry2.Term);
        Equal(1, await interpreter.InterpretAsync(entry2));
        Equal(-42, state.Value);

        var entry3 = interpreter.CreateLogEntry(new AssignCommand { Value = int.MaxValue }, 68L);
        Equal(68L, entry3.Term);
        Equal(3, await interpreter.InterpretAsync(entry3, string.Empty));
        Equal(int.MaxValue, state.Value);

        ValueTask BinaryOp(BinaryOperationCommand command, CancellationToken token) => CustomInterpreter.DoBinaryOperation(ref state.Value, command, token);

        ValueTask UnaryOp(UnaryOperationCommand command, CancellationToken token) => CustomInterpreter.DoUnaryOperation(ref state.Value, command, token);

        ValueTask AssignOp(AssignCommand command, object context, CancellationToken token)
        {
            NotNull(context);
            state.Value = command.Value;
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public static async Task InterpreterWithPersistentState()
    {
        using var wal = new TestPersistenceState();
        var entry1 = wal.CreateLogEntry(new BinaryOperationCommand { X = 44, Y = 2, Type = BinaryOperation.Subtract });
        await wal.AppendAsync(entry1);
        Equal(0, wal.Value);
        await wal.CommitAsync(CancellationToken.None);
        Equal(42, wal.Value);

        var entry2 = wal.CreateLogEntry(new UnaryOperationCommand { X = 42, Type = UnaryOperation.OnesComplement });
        await wal.AppendAsync(entry2);
        await wal.CommitAsync(CancellationToken.None);
        Equal(~42, wal.Value);

        var entry3 = wal.CreateLogEntry(new SnapshotCommand { Value = 56 });
        await wal.AppendAsync(entry3);
        await wal.CommitAsync(CancellationToken.None);
        Equal(56, wal.Value);
    }
}