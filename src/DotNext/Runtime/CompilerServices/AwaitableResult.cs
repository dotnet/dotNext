using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

/// <summary>
/// Represents the awaitable object that can suspend the exception raised by the underlying task.
/// </summary>
/// <typeparam name="T">The type of the task.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct AwaitableResult<T>
{
    private readonly ValueTask<T> task;

    internal AwaitableResult(Task<T> task)
        : this(new ValueTask<T>(task))
    {
    }

    internal AwaitableResult(ValueTask<T> task) => this.task = task;
    
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
    public AwaitableResult<T> ConfigureAwait(bool continueOnCapturedContext)
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
        private readonly ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter;

        internal Awaiter(in ValueTask<T> task, bool continueOnCapturedContext)
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
        public Result<T> GetResult()
        {
            Result<T> result;
            try
            {
                result = new(awaiter.GetResult());
            }
            catch (Exception e)
            {
                result = new(e);
            }

            return result;
        }
    }
}

/// <summary>
/// Represents the awaitable object that can suspend the exception raised by the underlying task.
/// </summary>
/// <typeparam name="T">The type of the task.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct AwaitableResult<T, TError>
    where TError : struct, Enum
{
    private readonly ValueTask<T> task;
    private readonly Converter<Exception, TError> converter;

    internal AwaitableResult(Task<T> task, Converter<Exception, TError> converter)
        : this(new ValueTask<T>(task), converter)
    {
    }

    internal AwaitableResult(ValueTask<T> task, Converter<Exception, TError> converter)
    {
        this.task = task;
        this.converter = converter;
    }
    
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
    public AwaitableResult<T, TError> ConfigureAwait(bool continueOnCapturedContext)
        => this with { ContinueOnCapturedContext = continueOnCapturedContext };

    /// <summary>
    /// Gets the awaiter for this object.
    /// </summary>
    /// <returns>The awaiter for this object.</returns>
    public Awaiter GetAwaiter() => new(task, converter, ContinueOnCapturedContext);

    /// <summary>
    /// Represents the awaiter that suspends exception.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter;
        private readonly Converter<Exception, TError> converter;

        internal Awaiter(in ValueTask<T> task, Converter<Exception, TError> converter, bool continueOnCapturedContext)
        {
            awaiter = task.ConfigureAwait(continueOnCapturedContext).GetAwaiter();
            this.converter = converter;
        }

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
        public Result<T, TError> GetResult()
        {
            Result<T, TError> result;
            try
            {
                result = new(awaiter.GetResult());
            }
            catch (Exception e)
            {
                result = new(converter(e));
            }

            return result;
        }
    }
}