using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks
{
    using Runtime.CompilerServices;

    /// <summary>
    /// Represents dynamically-typed task.
    /// </summary>
    /// <remarks>
    /// This type is helpful when actual result type of <see cref="Task{TResult}"/>
    /// is not known.
    /// Note that this type uses dynamic code compilation via DLR infrastructure.
    /// </remarks>
    [RuntimeFeatures(DynamicCodeCompilation = true)]
    [StructLayout(LayoutKind.Auto)]
    public readonly struct DynamicTaskAwaitable
    {
        private static readonly CallSite<Func<CallSite, Task, object>> GetResultCallSite = CallSite<Func<CallSite, Task, object>>.Create(new TaskResultBinder());

        /// <summary>
        /// Provides an object that waits for the completion of an asynchronous task.
        /// </summary>
        [StructLayout(LayoutKind.Auto)]
        public readonly struct Awaiter : IFuture
        {
            private readonly Task task;
            private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter;

            internal Awaiter(Task task, bool continueOnCaptureContext)
            {
                this.task = task;
                awaiter = task.ConfigureAwait(continueOnCaptureContext).GetAwaiter();
            }

            /// <summary>
            /// Gets a value that indicates whether the asynchronous task has completed.
            /// </summary>
            public bool IsCompleted => awaiter.IsCompleted;

            /// <summary>
            /// Sets the action to perform when this object stops waiting for the asynchronous task to complete.
            /// </summary>
            /// <param name="continuation">The action to perform when the wait operation completes.</param>
            public void OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

            /// <summary>
            /// Gets dynamically typed task result.
            /// </summary>
            /// <returns>The result of the completed task; or <see cref="System.Reflection.Missing.Value"/> if underlying task is not of type <see cref="Task{TResult}"/>.</returns>
            public dynamic GetResult() => DynamicTaskAwaitable.GetResult(task);
        }

        private readonly Task task;
        private readonly bool continueOnCaptureContext;

        internal DynamicTaskAwaitable(Task task, bool continueOnCaptureContext = true)
        {
            this.task = task;
            this.continueOnCaptureContext = continueOnCaptureContext;
        }

        /// <summary>
        /// Configures an awaiter used to await this task.
        /// </summary>
        /// <param name="continueOnCaptureContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
        /// <returns>An object used to await this task.</returns>
        public DynamicTaskAwaitable ConfigureAwait(bool continueOnCaptureContext) => new DynamicTaskAwaitable(task, continueOnCaptureContext);

        /// <summary>
        /// Gets an awaiter used to await this task.
        /// </summary>
        /// <returns>An awaiter instance.</returns>
        public Awaiter GetAwaiter() => new Awaiter(task, continueOnCaptureContext);

        internal static dynamic GetResult(Task task) => GetResultCallSite.Target.Invoke(GetResultCallSite, task);
    }
}
