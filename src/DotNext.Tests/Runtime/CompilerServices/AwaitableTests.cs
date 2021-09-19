using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class AwaitableTests : Test
    {
        [Fact]
        public static void TaskWithResultTest()
        {
            var task = Task<long>.Factory.StartNew(() => 42);
            task.Wait(DefaultTimeout);
            var awaiter = Awaitable<Task<long>, TaskAwaiter<long>, long>.GetAwaiter(task);
            True(NotifyCompletion<TaskAwaiter<long>>.IsCompleted(awaiter));
            Equal(42, Awaiter<TaskAwaiter<long>, long>.GetResult(awaiter));
        }

        public sealed class ValueHolder
        {
            public volatile int Value;

            public void ChangeValue() => Value = 42;
        }

        [Fact]
        public static void TaskWithoutResultTest()
        {
            var holder = new ValueHolder();
            var task = Task.Factory.StartNew(holder.ChangeValue);
            task.Wait(DefaultTimeout);
            var awaiter = Awaitable<Task, TaskAwaiter>.GetAwaiter(task);
            True(NotifyCompletion<TaskAwaiter>.IsCompleted(awaiter));
            Awaiter<TaskAwaiter>.GetResult(awaiter);
            Equal(42, holder.Value);
        }

        [Fact]
        public static async Task AwaitUsingConcept()
        {
            var task = new Task<int>(() => 42);
            task.Start();
            var result = await new Awaitable<Task<int>, TaskAwaiter<int>, int>(task);
            Equal(42, result);
        }
    }
}