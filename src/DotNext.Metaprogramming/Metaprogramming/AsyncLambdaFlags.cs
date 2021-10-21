namespace DotNext.Metaprogramming;

/// <summary>
/// Represents flags controlling dynamic compilation of asynchronous lambda expression.
/// </summary>
[Flags]
public enum AsyncLambdaFlags
{
    /// <summary>
    /// No special behavior must be applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// The returned type of the asynchronous lambda expression must be represented by
    /// <see cref="ValueTask"/> or <see cref="ValueTask{TResult}"/> instead of
    /// <see cref="Task"/> or <see cref="Task{TResult}"/>.
    /// </summary>
    UseValueTask = 1,

    /// <summary>
    /// Use task pooling instead of memory allocation.
    /// </summary>
    UseTaskPooling = UseValueTask << 1,
}