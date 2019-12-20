using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Tasks
{
    [ExcludeFromCodeCoverage]
    public sealed class ValueTaskSynchronizerTests : Assert
    {
        private sealed class SharedCounter
        {
            private long value = 0;

            internal void Inc() => value.IncrementAndGet();

            internal long Value => value;
        }

        private sealed class ValueTaskCompletionSource
        {
            private AsyncValueTaskMethodBuilder builder = AsyncValueTaskMethodBuilder.Create();

            internal void Complete() => builder.SetResult();

            internal ValueTask Task => builder.Task;
        }

        private sealed class ValueTaskCompletionSource<R>
        {
            private AsyncValueTaskMethodBuilder<R> builder = AsyncValueTaskMethodBuilder<R>.Create();

            internal void Complete(R result) => builder.SetResult(result);

            internal ValueTask<R> Task => builder.Task;
        }

        [Fact]
        public static async Task WhenAny()
        {
            var box = new StrongBox<int>(0);
            var source1 = new ValueTaskCompletionSource();
            var source2 = new ValueTaskCompletionSource();
            var source3 = new ValueTaskCompletionSource();
            ThreadPool.QueueUserWorkItem(state =>
            {
                box.Value.VolatileWrite(1);
                Thread.Sleep(50);
                source1.Complete();
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                box.Value.VolatileWrite(2);
                Thread.Sleep(200);
                source2.Complete();
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                box.Value.VolatileWrite(3);
                Thread.Sleep(150);
                source3.Complete();
            });
            var completedTask = await ValueTaskSynchronization.WhenAny(source1.Task, source2.Task, source3.Task);
            True(completedTask == source1.Task);
            False(completedTask == source2.Task);
            False(completedTask == source3.Task);
        }

        [Fact]
        public static async Task WhenAnyWithResult()
        {
            var source1 = new ValueTaskCompletionSource<int>();
            var source2 = new ValueTaskCompletionSource<int>();
            var source3 = new ValueTaskCompletionSource<int>();
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(50);
                source1.Complete(1);
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(200);
                source2.Complete(2);
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                Thread.Sleep(150);
                source3.Complete(3);
            });
            var completedTask = await ValueTaskSynchronization.WhenAny(source1.Task, source2.Task, source3.Task);
            True(completedTask == source1.Task);
            False(completedTask == source2.Task);
            False(completedTask == source3.Task);
            Equal(1, completedTask.Result);
        }

        [Fact]
        public static async Task WhenAll()
        {
            var counter = new SharedCounter();
            var source1 = new ValueTaskCompletionSource();
            var source2 = new ValueTaskCompletionSource();
            var source3 = new ValueTaskCompletionSource();
            ThreadPool.QueueUserWorkItem(state =>
            {
                counter.Inc();
                Thread.Sleep(100);
                source1.Complete();
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                counter.Inc();
                Thread.Sleep(200);
                source2.Complete();
            });
            ThreadPool.QueueUserWorkItem(state =>
            {
                counter.Inc();
                Thread.Sleep(150);
                source3.Complete();
            });
            await ValueTaskSynchronization.WhenAll(source1.Task, source2.Task, source3.Task);
            Equal(3, counter.Value);
        }
    }
}