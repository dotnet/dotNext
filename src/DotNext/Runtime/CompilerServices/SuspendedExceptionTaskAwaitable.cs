using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents awaitable object that can suspend exception raised by the underlying task.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct SuspendedExceptionTaskAwaitable
{
    private readonly ValueTask task;

    internal SuspendedExceptionTaskAwaitable(ValueTask task)
        => this.task = task;

    internal SuspendedExceptionTaskAwaitable(Task task)
        => this.task = new(task);

    internal bool ContinueOnCapturedContext
    {
        get;
        init;
    }

    internal Predicate<Exception>? Filter
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
    public SuspendedExceptionTaskAwaitable ConfigureAwait(bool continueOnCapturedContext)
        => this with { ContinueOnCapturedContext = continueOnCapturedContext };

    /// <summary>
    /// Gets the awaiter for this object.
    /// </summary>
    /// <returns>The awaiter for this object.</returns>
    public Awaiter GetAwaiter() => new(task, ContinueOnCapturedContext) { Filter = Filter };

    /// <summary>
    /// Represents the awaiter that suspends exception.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter;

        internal Awaiter(in ValueTask task, bool continueOnCapturedContext)
        {
            awaiter = task.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
        }

        internal Predicate<Exception>? Filter
        {
            private get;
            init;
        }

        /// <summary>
        /// Gets a value indicating that <see cref="SuspendedExceptionTaskAwaitable"/> has completed.
        /// </summary>
        public bool IsCompleted => awaiter.IsCompleted;

        /// <inheritdoc/>
        public void OnCompleted(Action action) => awaiter.OnCompleted(action);

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action action) => awaiter.UnsafeOnCompleted(action);

        /// <summary>
        /// Obtains a result of asynchronous operation, and suspends exception if needed.
        /// </summary>
        public void GetResult()
        {
            try
            {
                awaiter.GetResult();
            }
            catch (Exception e) when (Filter?.Invoke(e) ?? true)
            {
                // suspend exception
            }
        }
    }
}