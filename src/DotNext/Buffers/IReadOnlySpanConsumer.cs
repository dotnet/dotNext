using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

/// <summary>
/// Represents functional interface returning no value
/// and accepting the single argument of type <see cref="ReadOnlySpan{T}"/>.
/// </summary>
/// <remarks>
/// Functional interface can be used to pass
/// some application logic without heap allocation in
/// contrast to regulat delegates. Additionally, implementation
/// of functional interface may have encapsulated data acting
/// as closure which is not allocated on the heap.
/// </remarks>
/// <typeparam name="T">The type of the elements in the span.</typeparam>
public interface IReadOnlySpanConsumer<T> : ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>
{
    /// <summary>
    /// Invokes the consumer.
    /// </summary>
    /// <param name="span">The span of elements.</param>
    void Invoke(ReadOnlySpan<T> span);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                Invoke(input.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }
}

/// <summary>
/// Represents typed function pointer implementing <see cref="IReadOnlySpanConsumer{T}"/>.
/// </summary>
/// <remarks>
/// This type follows signature of <see cref="ReadOnlySpanAction{T, TArg}"/> delegate.
/// </remarks>
/// <typeparam name="T">The type of the elements in the span.</typeparam>
/// <typeparam name="TArg">The type of the argument to be passed to the function.</typeparam>
[CLSCompliant(false)]
[StructLayout(LayoutKind.Auto)]
public readonly unsafe struct ReadOnlySpanConsumer<T, TArg> : IReadOnlySpanConsumer<T>
{
    private readonly delegate*<ReadOnlySpan<T>, TArg, void> ptr;
    private readonly TArg arg;

    /// <summary>
    /// Wraps the function pointer and captures the argument.
    /// </summary>
    /// <param name="ptr">The function pointer.</param>
    /// <param name="arg">The argument to be passed to the function represented by the function pointer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="ptr"/> is zero.</exception>
    public ReadOnlySpanConsumer(delegate*<ReadOnlySpan<T>, TArg, void> ptr, TArg arg)
    {
        this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));
        this.arg = arg;
    }

    /// <summary>
    /// Gets a value indicating that this function pointer is zero.
    /// </summary>
    public bool IsEmpty => ptr is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => ptr(span, arg);
}

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer{T}"/> that delegates
/// invocation to the delegate of type <see cref="ReadOnlySpanAction{T, TArg}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
/// <typeparam name="TArg">The type of the argument to be passed to the delegate.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct DelegatingReadOnlySpanConsumer<T, TArg> : IReadOnlySpanConsumer<T>
{
    private readonly ReadOnlySpanAction<T, TArg> action;
    private readonly TArg arg;

    /// <summary>
    /// Wraps the delegate instance.
    /// </summary>
    /// <param name="action">The delegate instance.</param>
    /// <param name="arg">The argument to be passed to the function represented by the delegate.</param>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is <see langword="null"/>.</exception>
    public DelegatingReadOnlySpanConsumer(ReadOnlySpanAction<T, TArg> action, TArg arg)
    {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
        this.arg = arg;
    }

    /// <summary>
    /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => action is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => action(span, arg);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                action(input.Span, arg);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }
}

/// <summary>
/// Represents implementation of <see cref="IReadOnlySpanConsumer{T}"/>
/// in the form of the writer to <see cref="IBufferWriter{T}"/>.
/// </summary>
/// <typeparam name="T">The type of the consumer argument.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly record struct BufferConsumer<T> : IReadOnlySpanConsumer<T>, IEquatable<BufferConsumer<T>>
{
    private readonly IBufferWriter<T> output;

    /// <summary>
    /// Wraps the buffer writer.
    /// </summary>
    /// <param name="output">The buffer writer.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public BufferConsumer(IBufferWriter<T> output) => this.output = output ?? throw new ArgumentNullException(nameof(output));

    /// <summary>
    /// Gets a value indicating that the underlying buffer is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => output is null;

    /// <inheritdoc />
    void IReadOnlySpanConsumer<T>.Invoke(ReadOnlySpan<T> span) => output.Write(span);

    /// <inheritdoc />
    ValueTask ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>.Invoke(ReadOnlyMemory<T> input, CancellationToken token)
    {
        ValueTask result;
        if (token.IsCancellationRequested)
        {
            result = ValueTask.FromCanceled(token);
        }
        else
        {
            result = new();
            try
            {
                output.Write(input.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <summary>
    /// Returns a string that represents the underlying buffer.
    /// </summary>
    /// <returns>A string that represents the underlying buffer.</returns>
    public override string? ToString() => output?.ToString();
}