namespace DotNext.Buffers;

/// <summary>
/// Represents <see cref="System.Buffers.ReadOnlySpanAction{T, TArg}"/> counterpart
/// with return value.
/// </summary>
/// <typeparam name="T">The type of the objects in the read-only span.</typeparam>
/// <typeparam name="TArg">The type of the object that represents the state.</typeparam>
/// <typeparam name="TResult">The type of return value.</typeparam>
/// <param name="span">A read-only span of objects.</param>
/// <param name="arg">A state object.</param>
/// <returns>The value returned by the delegate.</returns>
public delegate TResult ReadOnlySpanFunc<T, TArg, TResult>(ReadOnlySpan<T> span, TArg arg);