using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents the awaitable object that returns an exception if it was thrown by the task, or <see langword="null"/>.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SilentAwaitable
{
    private readonly ValueTask task;

    internal SilentAwaitable(Task task)
        : this(new ValueTask(task))
    {
    }

    internal SilentAwaitable(ValueTask task) => this.task = task;
    
    internal bool ContinueOnCapturedContext
    {
        get;
        init;
    }
    
    /// <summary>
    /// Configures an awaiter for this value.
    /// </summary>
    /// <param name="continueOnCapturedContext">
    /// <see langword="true"/> to attempt to marshal the continuation back to the captured context;
    /// otherwise, <see langword="false"/>.
    /// </param>
    /// <returns>The configured object.</returns>
    public SilentAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => this with { ContinueOnCapturedContext = continueOnCapturedContext };

    /// <summary>
    /// Gets the awaiter for this object.
    /// </summary>
    /// <returns>The awaiter for this object.</returns>
    public Awaiter GetAwaiter() => new(task, ContinueOnCapturedContext);

    /// <summary>
    /// Represents the awaiter that suspends exception.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter;

        internal Awaiter(in ValueTask task, bool continueOnCapturedContext)
            => awaiter = task.ConfigureAwait(continueOnCapturedContext).GetAwaiter();

        /// <summary>
        /// Gets a value indicating that <see cref="AwaitableResult{T}"/> has completed.
        /// </summary>
        public bool IsCompleted => awaiter.IsCompleted;

        /// <inheritdoc/>
        public void OnCompleted(Action action) => awaiter.OnCompleted(action);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action action) => awaiter.UnsafeOnCompleted(action);

        /// <summary>
        /// Gets a result of asynchronous operation, and suspends exception if needed.
        /// </summary>
        public Exception? GetResult()
        {
            var result = default(Exception);
            try
            {
                awaiter.GetResult();
            }
            catch (Exception e)
            {
                result = e;
            }

            return result;
        }
    }
}