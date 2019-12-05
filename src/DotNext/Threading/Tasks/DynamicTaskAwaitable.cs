using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks
{
    using TaskResultBinder = Runtime.CompilerServices.TaskResultBinder;

    public readonly struct DynamicTaskAwaitable
    {
        private static readonly CallSite<Func<CallSite, Task, object>> GetResultCallSite = CallSite<Func<CallSite, Task, object>>.Create(new TaskResultBinder());

        public readonly struct Awaiter : IFuture
        {
            private readonly Task task;
            private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter;

            internal Awaiter(Task task, bool continueOnCaptureContext)
            {
                this.task = task;
                awaiter = task.ConfigureAwait(continueOnCaptureContext).GetAwaiter();
            }

            public bool IsCompleted => awaiter.IsCompleted;

            public void OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

            public dynamic GetResult() => DynamicTaskAwaitable.GetResult(task);
        }

        private readonly Task task;
        private readonly bool continueOnCaptureContext;

        internal DynamicTaskAwaitable(Task task, bool continueOnCaptureContext = true)
        {
            this.task = task;
            this.continueOnCaptureContext = continueOnCaptureContext;
        }

        public DynamicTaskAwaitable ConfigureAwait(bool continueOnCaptureContext) => new DynamicTaskAwaitable(task, continueOnCaptureContext);

        public Awaiter GetAwaiter() => new Awaiter(task, continueOnCaptureContext);

        internal static dynamic GetResult(Task task) => GetResultCallSite.Target.Invoke(GetResultCallSite, task);
    }
}
