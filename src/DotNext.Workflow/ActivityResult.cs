using System.Runtime.CompilerServices;

namespace DotNext.Workflow;

[AsyncMethodBuilder(typeof(ActivityStateHandler))]
public readonly struct ActivityResult
{
    public readonly struct Awaiter : INotifyCompletion, ICriticalNotifyCompletion
    {
        private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter;

        internal Awaiter(ConfiguredTaskAwaitable awaitable) => awaiter = awaitable.GetAwaiter();

        public bool IsCompleted => awaiter.IsCompleted;

        public void GetResult() => awaiter.GetResult();

        void INotifyCompletion.OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        void ICriticalNotifyCompletion.UnsafeOnCompleted(Action continuation) => awaiter.UnsafeOnCompleted(continuation);
    }

    internal readonly Task Task;

    internal ActivityResult(Task task) => Task = task;

    public Awaiter GetAwaiter() => new(Task.ConfigureAwait(false));
}