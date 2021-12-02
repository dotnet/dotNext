using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks;

internal abstract class LinkedValueTaskCompletionSource<T> : ValueTaskCompletionSource<T>
{
    private LinkedValueTaskCompletionSource<T>? previous, next;

    private protected LinkedValueTaskCompletionSource(bool runContinuationsAsynchronously = true)
        : base(runContinuationsAsynchronously)
    {
    }

    internal LinkedValueTaskCompletionSource<T>? Next => next;

    internal LinkedValueTaskCompletionSource<T>? Previous => previous;

    internal bool IsNotRoot => next is not null || previous is not null;

    internal void Append(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(next is null);

        node.previous = this;
        next = node;
    }

    internal void Prepend(LinkedValueTaskCompletionSource<T> node)
    {
        Debug.Assert(previous is null);

        node.next = this;
        previous = node;
    }

    internal void Detach()
    {
        if (previous is not null)
            previous.next = next;
        if (next is not null)
            next.previous = previous;
        next = previous = null;
    }

    internal LinkedValueTaskCompletionSource<T>? CleanupAndGotoNext()
    {
        var next = this.next;
        this.next = previous = null;
        return next;
    }
}