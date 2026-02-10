using System.Buffers;
using System.Runtime.InteropServices;

namespace DotNext.Buffers;

using Runtime;
using Runtime.CompilerServices;

internal interface IReadOnlySpanConsumer<T> : IConsumer<ReadOnlySpan<T>>, ISupplier<ReadOnlyMemory<T>, CancellationToken, ValueTask>
{
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
    
    /// <inheritdoc cref="IFunctional.DynamicInvoke"/>
    void IFunctional.DynamicInvoke(ref readonly Variant args, int count, scoped Variant result)
    {
        switch (count)
        {
            case 1:
                Invoke(args.Immutable<ReadOnlySpan<T>>());
                break;
            case 2:
                result.Mutable<ValueTask>() = Invoke(
                    GetArgument<ReadOnlyMemory<T>>(in args, 0),
                    GetArgument<CancellationToken>(in args, 1)
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}

/// <summary>
/// Represents implementation of <see cref="IConsumer{T}"/> and <see cref="ISupplier{T1, T2, TResult}"/> interfaces
/// that delegates invocation to the delegate of type <see cref="ReadOnlySpanAction{T,TArg}"/>.
/// </summary>
/// <param name="action">The delegate instance.</param>
/// <param name="context">The argument to be passed to the function represented by the delegate.</param>
/// <typeparam name="T">The type of the span elements.</typeparam>
/// <typeparam name="TContext">The type of the argument to be passed to the delegate.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct ReadOnlySpanConsumer<T, TContext>(ReadOnlySpanAction<T, TContext> action, TContext context) : IReadOnlySpanConsumer<T>
{
    /// <summary>
    /// Gets a value indicating that the underlying delegate is <see langword="null"/>.
    /// </summary>
    public bool IsEmpty => action is null;

    /// <inheritdoc/>
    void IConsumer<ReadOnlySpan<T>>.Invoke(ReadOnlySpan<T> span) => action(span, context);
}