using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

using Dynamic;
using static Reflection.TaskType;
using RuntimeFeaturesAttribute = Runtime.CompilerServices.RuntimeFeaturesAttribute;

/// <summary>
/// Represents dynamically-typed task.
/// </summary>
/// <remarks>
/// This type is helpful when actual result type of <see cref="Task{TResult}"/>
/// is not known.
/// Note that this type uses dynamic code compilation via DLR infrastructure.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public readonly struct DynamicTaskAwaitable
{
    private static readonly CallSite<Func<CallSite, Task, object?>> GetResultCallSite = CallSite<Func<CallSite, Task, object?>>.Create(new TaskResultBinder());

    /// <summary>
    /// Provides an object that waits for the completion of an asynchronous task.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    [RuntimeFeatures(DynamicCodeCompilation = true)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
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

        /// <inheritdoc />
        void ICriticalNotifyCompletion.UnsafeOnCompleted(Action continuation)
            => awaiter.UnsafeOnCompleted(continuation);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTaskWithResult(Type type)
            => type != CompletedTaskType && type.IsConstructedGenericType;

        internal object? GetRawResult()
            => IsTaskWithResult(task.GetType()) ?
                GetResultCallSite.Target.Invoke(GetResultCallSite, task) :
                Missing.Value;

        /// <summary>
        /// Gets dynamically typed task result.
        /// </summary>
        /// <returns>The result of the completed task; or <see cref="System.Reflection.Missing.Value"/> if underlying task is not of type <see cref="Task{TResult}"/>.</returns>
        public dynamic? GetResult()
        {
            if (IsTaskWithResult(task.GetType()))
                return GetResultCallSite.Target.Invoke(GetResultCallSite, task);

            awaiter.GetResult();
            return Missing.Value;
        }
    }

    private readonly Task task;
    private readonly bool continueOnCapturedContext;

    internal DynamicTaskAwaitable(Task task, bool continueOnCapturedContext = true)
    {
        this.task = task;
        this.continueOnCapturedContext = continueOnCapturedContext;
    }

    /// <summary>
    /// Configures an awaiter used to await this task.
    /// </summary>
    /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
    /// <returns>An object used to await this task.</returns>
    public DynamicTaskAwaitable ConfigureAwait(bool continueOnCapturedContext) => new(task, continueOnCapturedContext);

    /// <summary>
    /// Gets an awaiter used to await this task.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    public Awaiter GetAwaiter() => new(task, continueOnCapturedContext);
}