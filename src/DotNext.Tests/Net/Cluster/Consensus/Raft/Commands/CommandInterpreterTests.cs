using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    using Runtime.Serialization;

    [ExcludeFromCodeCoverage]
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

        [Command(0, Formatter = typeof(Formatter))]
        private struct BinaryOperationCommand
        {
            public int X, Y;
            public BinaryOperation Type;
        }

        [Command(1, Formatter = typeof(Formatter), FormatterMember = nameof(Formatter.Instance))]
        private struct UnaryOperationCommand
        {
            public int X;
            public UnaryOperation Type;
        }

        [Command(3)]
        private struct AssignCommand
        {
            public int Value;
        }

        [Command(4, Formatter = typeof(Formatter), FormatterMember = nameof(Formatter.Instance))]
        private struct SnapshotCommand
        {
            public int Value;
        }

        private sealed class Formatter : IFormatter<BinaryOperationCommand>, IFormatter<UnaryOperationCommand>, IFormatter<AssignCommand>, IFormatter<SnapshotCommand>
        {
            public static readonly Formatter Instance = new();

            async ValueTask IFormatter<BinaryOperationCommand>.SerializeAsync<TWriter>(BinaryOperationCommand command, TWriter writer, CancellationToken token)
            {
                await writer.WriteInt32Async(command.X, true, token);
                await writer.WriteInt32Async(command.Y, true, token);
                await writer.WriteAsync(command.Type, token);
            }

            async ValueTask<BinaryOperationCommand> IFormatter<BinaryOperationCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            {
                return new BinaryOperationCommand
                {
                    X = await reader.ReadInt32Async(true, token),
                    Y = await reader.ReadInt32Async(true, token),
                    Type = await reader.ReadAsync<BinaryOperation>(token)
                };
            }

            unsafe long? IFormatter<BinaryOperationCommand>.GetLength(BinaryOperationCommand command)
                => sizeof(BinaryOperationCommand);

            async ValueTask IFormatter<UnaryOperationCommand>.SerializeAsync<TWriter>(UnaryOperationCommand command, TWriter writer, CancellationToken token)
            {
                await writer.WriteInt32Async(command.X, true, token);
                await writer.WriteAsync(command.Type, token);
            }

            async ValueTask<UnaryOperationCommand> IFormatter<UnaryOperationCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            {
                return new UnaryOperationCommand
                {
                    X = await reader.ReadInt32Async(true, token),
                    Type = await reader.ReadAsync<UnaryOperation>(token)
                };
            }

            unsafe long? IFormatter<UnaryOperationCommand>.GetLength(UnaryOperationCommand command)
                => sizeof(UnaryOperationCommand);

            ValueTask IFormatter<AssignCommand>.SerializeAsync<TWriter>(AssignCommand command, TWriter writer, CancellationToken token)
                => writer.WriteAsync(command, token);

            ValueTask<AssignCommand> IFormatter<AssignCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
                => reader.ReadAsync<AssignCommand>(token);

            unsafe long? IFormatter<AssignCommand>.GetLength(AssignCommand command)
                => sizeof(AssignCommand);

            ValueTask IFormatter<SnapshotCommand>.SerializeAsync<TWriter>(SnapshotCommand command, TWriter writer, CancellationToken token)
                => writer.WriteAsync(command, token);

            ValueTask<SnapshotCommand> IFormatter<SnapshotCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
                => reader.ReadAsync<SnapshotCommand>(token);

            unsafe long? IFormatter<SnapshotCommand>.GetLength(SnapshotCommand command)
                => sizeof(SnapshotCommand);
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
            public ValueTask DoBinaryOperation(BinaryOperationCommand command, CancellationToken token)
                => DoBinaryOperation(ref Value, command, token);

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

        private sealed class TestPersistenceState : PersistentState
        {
            private readonly CustomInterpreter interpreter;

            public TestPersistenceState()
                : base(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()), 4)
            {
                interpreter = new CustomInterpreter();
            }

            internal int Value => interpreter.Value;

            internal LogEntry<TCommand> CreateLogEntry<TCommand>(TCommand command)
                where TCommand : struct
                => interpreter.CreateLogEntry(command, Term);

            protected override ValueTask ApplyAsync(LogEntry entry)
                => new(interpreter.InterpretAsync(entry).AsTask());

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
            Func<BinaryOperationCommand, CancellationToken, ValueTask> binaryOp = (command, token) => CustomInterpreter.DoBinaryOperation(ref state.Value, command, token);
            Func<UnaryOperationCommand, CancellationToken, ValueTask> unaryOp = (command, token) => CustomInterpreter.DoUnaryOperation(ref state.Value, command, token);
            Func<AssignCommand, CancellationToken, ValueTask> assignOp = (command, token) =>
            {
                state.Value = command.Value;
                return new ValueTask();
            };

            var interpreter = new CommandInterpreter.Builder()
                .Add(binaryOp)
                .Add(unaryOp)
                .Add(assignOp, new Formatter())
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
            Equal(3, await interpreter.InterpretAsync(entry3));
            Equal(int.MaxValue, state.Value);
        }

        [Fact]
        public static async Task InterpreterWithPersistentState()
        {
            await using var wal = new TestPersistenceState();
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
}