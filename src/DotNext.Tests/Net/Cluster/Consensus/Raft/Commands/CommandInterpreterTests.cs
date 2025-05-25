using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using IO;
using StateMachine;

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

    private struct BinaryOperationCommand : ICommand<BinaryOperationCommand>
    {
        public static int Id => 0;

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
            => new()
            {
                X = await reader.ReadLittleEndianAsync<int>(token),
                Y = await reader.ReadLittleEndianAsync<int>(token),
                Type = (BinaryOperation)await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct UnaryOperationCommand : ICommand<UnaryOperationCommand>
    {
        public static int Id => 1;

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
            => new()
            {
                X = await reader.ReadLittleEndianAsync<int>(token),
                Type = (UnaryOperation)await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct AssignCommand : ICommand<AssignCommand>
    {
        public static int Id => 3;

        public int Value;

        readonly long? IDataTransferObject.Length => sizeof(int);

        readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteLittleEndianAsync(Value, token);

        public static async ValueTask<AssignCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new()
            {
                Value = await reader.ReadLittleEndianAsync<int>(token),
            };
    }

    private struct SnapshotCommand : ICommand<SnapshotCommand>
    {
        public static int Id => 4;

        public int Value;

        readonly long? IDataTransferObject.Length => sizeof(int);

        static bool ICommand<SnapshotCommand>.IsSnapshot => true;

        readonly ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
            => writer.WriteLittleEndianAsync(Value, token);

        public static async ValueTask<SnapshotCommand> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
            where TReader : notnull, IAsyncBinaryReader
            => new()
            {
                Value = await reader.ReadLittleEndianAsync<int>(token),
            };
    }
    
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

        [CommandHandler]
        public ValueTask ApplySnapshot(SnapshotCommand command, CancellationToken token)
        {
            Value = command.Value;
            return token.IsCancellationRequested ? new ValueTask(Task.FromCanceled(token)) : new ValueTask();
        }
    }
    
    [Experimental("DOTNEXT001")]
    private sealed class SimpleStateMachine : NoOpSnapshotManager, IStateMachine
    {
        private readonly CustomInterpreter interpreter = new();

        public async ValueTask<long> ApplyAsync(LogEntry entry, CancellationToken token)
        {
            await interpreter.InterpretAsync(entry, token);
            return entry.Index;
        }

        internal int Value => interpreter.Value;
    }

    [Fact]
    public static async Task MethodsAsHandlers()
    {
        using var interpreter = new CustomInterpreter();
        var entry1 = new LogEntry<BinaryOperationCommand>()
        {
            Command = new() { X = 40, Y = 2, Type = BinaryOperation.Add },
            Term = 1L,
        };
        
        Equal(1L, entry1.Term);
        Equal(0, interpreter.Value);
        Equal(0, await interpreter.InterpretAsync(entry1));
        Equal(42, interpreter.Value);

        var entry2 = new LogEntry<UnaryOperationCommand>()
        {
            Command = new() { X = 42, Type = UnaryOperation.Negate },
            Term = 10L,
        };
        
        Equal(10L, entry2.Term);
        Equal(1, await interpreter.InterpretAsync(entry2));
        Equal(-42, interpreter.Value);
    }

    [Fact]
    public static async Task DelegatesAsHandlers()
    {
        var state = new StrongBox<int>();

        var interpreter = new CommandInterpreter.Builder()
            .Add(new Func<BinaryOperationCommand, CancellationToken, ValueTask>(BinaryOp))
            .Add(new Func<UnaryOperationCommand, CancellationToken, ValueTask>(UnaryOp))
            .Add(new Func<AssignCommand, object, CancellationToken, ValueTask>(AssignOp))
            .Build();

        var entry1 = new LogEntry<BinaryOperationCommand>()
        {
            Command = new() { X = 40, Y = 2, Type = BinaryOperation.Add },
            Term = 1L,
        };
        
        Equal(1L, entry1.Term);
        Equal(0, state.Value);
        Equal(0, await interpreter.InterpretAsync(entry1));
        Equal(42, state.Value);

        var entry2 = new LogEntry<UnaryOperationCommand>()
        {
            Command = new() { X = 42, Type = UnaryOperation.Negate },
            Term = 10L,
        };
        
        Equal(10L, entry2.Term);
        Equal(1, await interpreter.InterpretAsync(entry2));
        Equal(-42, state.Value);

        var entry3 = new LogEntry<AssignCommand>()
        {
            Command = new() { Value = int.MaxValue },
            Term = 68L,
        };
        
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
    [Experimental("DOTNEXT001")]
    public static async Task InterpreterWithPersistentState()
    {
        var stateMachine = new SimpleStateMachine();
        await using var wal = new WriteAheadLog(new() { Location = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()) }, stateMachine);
        var index = await wal.AppendAsync(new BinaryOperationCommand { X = 44, Y = 2, Type = BinaryOperation.Subtract });
        Equal(0, stateMachine.Value);
        await wal.CommitAsync(index);
        await wal.WaitForApplyAsync(index);
        Equal(42, stateMachine.Value);

        index = await wal.AppendAsync(new UnaryOperationCommand { X = 42, Type = UnaryOperation.OnesComplement });
        await wal.CommitAsync(index);
        await wal.WaitForApplyAsync(index);
        Equal(~42, stateMachine.Value);
    }
}