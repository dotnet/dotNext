using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

using Dynamic;
using static Reflection.TaskType;

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
    /// <summary>
    /// Provides an object that waits for the completion of an asynchronous task.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private static CallSite<Func<CallSite, Task, object?>>? getResultCallSite;

        private readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter;

        internal Awaiter(Task task, ConfigureAwaitOptions options)
            => awaiter = task.ConfigureAwait(options).GetAwaiter();

        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "m_task")]
        private static extern ref readonly Task GetTask(ref readonly ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter);

        /// <summary>
        /// Gets a value that indicates whether the asynchronous task has completed.
        /// </summary>
        public bool IsCompleted => awaiter.IsCompleted;

        /// <inheritdoc cref="INotifyCompletion.OnCompleted"/>
        public void OnCompleted(Action continuation) => awaiter.OnCompleted(continuation);

        /// <inheritdoc cref="ICriticalNotifyCompletion.UnsafeOnCompleted" />
        public void UnsafeOnCompleted(Action continuation)
            => awaiter.UnsafeOnCompleted(continuation);

        [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
        internal object? GetRawResult()
        {
            awaiter.GetResult();
            var task = GetTask(in awaiter);

            return IsTaskWithResult(task.GetType()) ? GetDynamicResult(task) : Missing.Value;
            
            [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
            static object? GetDynamicResult(Task task)
            {
                var callSite = getResultCallSite ??= CallSite<Func<CallSite, Task, object?>>.Create(new TaskResultBinder());
                return callSite.Target(callSite, task);
            }
            
            static bool IsTaskWithResult(Type type)
                => type != CompletedTaskType && type.IsConstructedGenericType;
        }

        /// <summary>
        /// Gets dynamically typed task result.
        /// </summary>
        /// <returns>The result of the completed task; or <see cref="Missing.Value"/> if underlying task is not of type <see cref="Task{TResult}"/>.</returns>
        [RequiresUnreferencedCode("Runtime binding may be incompatible with IL trimming")]
        public dynamic? GetResult() => GetRawResult();
    }

    private readonly Task task;
    private readonly ConfigureAwaitOptions options;

    internal DynamicTaskAwaitable(Task task, ConfigureAwaitOptions options = ConfigureAwaitOptions.ContinueOnCapturedContext)
    {
        this.task = task;
        this.options = options;
    }

    /// <summary>
    /// Configures an awaiter used to await this task.
    /// </summary>
    /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
    /// <returns>An object used to await this task.</returns>
    public DynamicTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => new(task, continueOnCapturedContext
            ? options | ConfigureAwaitOptions.ContinueOnCapturedContext
            : options & ~ConfigureAwaitOptions.ContinueOnCapturedContext);

    /// <summary>
    /// Configures an awaiter used to await this task.
    /// </summary>
    /// <param name="options">Options used to configure how awaits on this task are performed.</param>
    /// <returns>Configured awaitable object.</returns>
    public DynamicTaskAwaitable ConfigureAwait(ConfigureAwaitOptions options) => new(task, options);

    /// <summary>
    /// Gets an awaiter used to await this task.
    /// </summary>
    /// <returns>An awaiter instance.</returns>
    public Awaiter GetAwaiter() => new(task, options);
}