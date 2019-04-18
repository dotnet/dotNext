using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotNext.Runtime.CompilerServices
{
    public sealed class AwaitableTests: Assert
    {
        [Fact]
        public static void TaskWithResultTest()
        {
            var task = Task<long>.Factory.StartNew(() => 42);
            task.Wait();
            var awaiter = Awaitable<Task<long>, TaskAwaiter<long>, long>.GetAwaiter(task);
            True(Awaiter<TaskAwaiter<long>, long>.IsCompleted(awaiter));
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
            task.Wait();
            var awaiter = Awaitable<Task, TaskAwaiter>.GetAwaiter(task);
            True(Awaiter<TaskAwaiter>.IsCompleted(awaiter));
            Awaiter<TaskAwaiter>.GetResult(awaiter);
            Equal(42, holder.Value);
        }
    }
}