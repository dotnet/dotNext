using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
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

        [Command(1, Formatter = typeof(Formatter))]
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

        private sealed class Formatter : ICommandFormatter<BinaryOperationCommand>, ICommandFormatter<UnaryOperationCommand>, ICommandFormatter<AssignCommand>
        {
            async ValueTask ICommandFormatter<BinaryOperationCommand>.SerializeAsync<TWriter>(BinaryOperationCommand command, TWriter writer, CancellationToken token)
            {
                await writer.WriteInt32Async(command.X, true, token);
                await writer.WriteInt32Async(command.Y, true, token);
                await writer.WriteAsync(command.Type, token);
            }

            async ValueTask<BinaryOperationCommand> ICommandFormatter<BinaryOperationCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            {
                return new BinaryOperationCommand
                {
                    X = await reader.ReadInt32Async(true, token),
                    Y = await reader.ReadInt32Async(true, token),
                    Type = await reader.ReadAsync<BinaryOperation>(token)
                };
            }

            unsafe long? ICommandFormatter<BinaryOperationCommand>.GetLength(in BinaryOperationCommand command)
                => sizeof(BinaryOperationCommand);

            async ValueTask ICommandFormatter<UnaryOperationCommand>.SerializeAsync<TWriter>(UnaryOperationCommand command, TWriter writer, CancellationToken token)
            {
                await writer.WriteInt32Async(command.X, true, token);
                await writer.WriteAsync(command.Type, token);
            }

            async ValueTask<UnaryOperationCommand> ICommandFormatter<UnaryOperationCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
            {
                return new UnaryOperationCommand
                {
                    X = await reader.ReadInt32Async(true, token),
                    Type = await reader.ReadAsync<UnaryOperation>(token)
                };
            }

            unsafe long? ICommandFormatter<UnaryOperationCommand>.GetLength(in UnaryOperationCommand command)
                => sizeof(UnaryOperationCommand);

            ValueTask ICommandFormatter<AssignCommand>.SerializeAsync<TWriter>(AssignCommand command, TWriter writer, CancellationToken token)
                => writer.WriteAsync(command, token);

            ValueTask<AssignCommand> ICommandFormatter<AssignCommand>.DeserializeAsync<TReader>(TReader reader, CancellationToken token)
                => reader.ReadAsync<AssignCommand>(token);

            unsafe long? ICommandFormatter<AssignCommand>.GetLength(in AssignCommand command)
                => sizeof(AssignCommand);
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

                return new ValueTask();
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

                return new ValueTask();
            }

            [CommandHandler]
            public ValueTask DoUnaryOperation(UnaryOperationCommand command, CancellationToken token)
                => DoUnaryOperation(ref Value, command, token);
        }

        [Fact]
        public static async Task MethodsAsHandlers()
        {
            using var interpreter = new CustomInterpreter();
            var entry1 = interpreter.CreateLogEntry(new BinaryOperationCommand { X = 40, Y = 2, Type = BinaryOperation.Add}, 1L);
            Equal(1L, entry1.Term);
            Equal(0, interpreter.Value);
            Equal(0, await interpreter.InterpretAsync(entry1));
            Equal(42, interpreter.Value);

            interpreter.Value = 0;
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

            var entry1 = interpreter.CreateLogEntry(new BinaryOperationCommand { X = 40, Y = 2, Type = BinaryOperation.Add}, 1L);
            Equal(1L, entry1.Term);
            Equal(0, state.Value);
            Equal(0, await interpreter.InterpretAsync(entry1));
            Equal(42, state.Value);

            state.Value = 0;
            var entry2 = interpreter.CreateLogEntry(new UnaryOperationCommand { X = 42, Type = UnaryOperation.Negate }, 10L);
            Equal(10L, entry2.Term);
            Equal(1, await interpreter.InterpretAsync(entry2));
            Equal(-42, state.Value);

            var entry3 = interpreter.CreateLogEntry(new AssignCommand { Value = int.MaxValue }, 68L);
            Equal(68L, entry3.Term);
            Equal(3, await interpreter.InterpretAsync(entry3));
            Equal(int.MaxValue, state.Value);
        }
    }
}