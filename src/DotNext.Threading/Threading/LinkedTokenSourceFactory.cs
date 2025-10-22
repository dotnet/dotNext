using DotNext.Buffers;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading;

/// <summary>
/// Represents helper methods for working with linked cancellation tokens.
/// </summary>
public static class LinkedTokenSourceFactory
{
    /// <summary>
    /// Links two cancellation tokens together.
    /// </summary>
    /// <param name="first">The first cancellation token. Can be modified by this method.</param>
    /// <param name="second">The second cancellation token.</param>
    /// <returns>The linked token source; or <see langword="null"/> if <paramref name="first"/> or <paramref name="second"/> is not cancelable.</returns>
    [Obsolete("Use CancellationTokenMultiplexer class instead.")]
    public static LinkedCancellationTokenSource? LinkTo(this ref CancellationToken first, CancellationToken second)
        => LinkedCancellationTokenSource.Combine(ref first, second);

    /// <summary>
    /// Links multiple cancellation tokens together.
    /// </summary>
    /// <param name="first">The first cancellation token. Can be modified by this method.</param>
    /// <param name="tokens">A list of cancellation tokens to link together.</param>
    /// <returns>The linked token source; or <see langword="null"/> if <paramref name="first"/> or <paramref name="tokens"/> are not cancelable.</returns>
    [Obsolete("Use CancellationTokenMultiplexer class instead.")]
    public static LinkedCancellationTokenSource? LinkTo(this ref CancellationToken first, ReadOnlySpan<CancellationToken> tokens)
    {
        LinkedCancellationTokenSource? result;
        if (tokens.IsEmpty)
        {
            result = null;
        }
        else
        {
            result = new MultipleLinkedCancellationTokenSource(tokens, out var isEmpty, first);
            if (isEmpty)
            {
                result.Dispose();
                result = null;
            }
            else
            {
                first = result.Token;
            }
        }

        return result;
    }

    /// <summary>
    /// Links cancellation token with the timeout.
    /// </summary>
    /// <param name="token">The first cancellation token. Can be modified by this method.</param>
    /// <param name="timeout">The timeout to link.</param>
    /// <returns>The linked token source; or <see langword="null"/> if <paramref name="token"/> is canceled or <paramref name="timeout"/> is <see cref="System.Threading.Timeout.InfiniteTimeSpan"/>.</returns>
    [Obsolete("Use CancellationTokenMultiplexer class instead.")]
    public static CancellationTokenSource? LinkTo(this ref CancellationToken token, TimeSpan timeout)
    {
        CancellationTokenSource? result;

        if (token.IsCancellationRequested || timeout < default(TimeSpan))
        {
            result = null;
        }
        else
        {
            result = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token) : new CancellationTokenSource();
            result.CancelAfter(timeout);
            token = result.Token;
        }

        return result;
    }

    /// <summary>
    /// Links two cancellation tokens with the given timeout.
    /// </summary>
    /// <param name="first">The first cancellation token. Can be modified by this method.</param>
    /// <param name="timeout">The timeout to link.</param>
    /// <param name="second">The second cancellation token.</param>
    /// <returns>The linked token source; or <see langword="null"/> if not needed.</returns>
    [Obsolete("Use CancellationTokenMultiplexer class instead.")]
    public static CancellationTokenSource? LinkTo(this ref CancellationToken first, TimeSpan timeout, CancellationToken second)
    {
        CancellationTokenSource? result;

        if (first == second)
        {
            result = LinkTo(ref first, timeout);
        }
        else if (timeout < TimeSpan.Zero || !second.CanBeCanceled || first.IsCancellationRequested || second.IsCancellationRequested)
        {
            result = LinkedCancellationTokenSource.Combine(ref first, second);
        }
        else if (!first.CanBeCanceled)
        {
            result = LinkTo(ref second, timeout);
            first = second;
        }
        else
        {
            result = null;
        }

        return result;
    }

    /// <summary>
    /// Determines whether the operation was canceled by the specified source.
    /// </summary>
    /// <param name="source">The linked token source.</param>
    /// <param name="e">The exception to analyze.</param>
    /// <param name="token">The token to check</param>
    /// <returns><see langword="true"/> indicates that the cancellation caused by <paramref name="source"/> and <see cref="LinkedCancellationTokenSource.CancellationOrigin"/> is <paramref name="token"/> ;or by <paramref name="token"/>.</returns>
    public static bool CausedBy<TSource>(this OperationCanceledException e, TSource? source, CancellationToken token)
        where TSource : IMultiplexedCancellationTokenSource?
        => source is null ? e.CancellationToken == token : e.CancellationToken == source.Token && source.CancellationOrigin == token;
    
    [Obsolete]
    private sealed class MultipleLinkedCancellationTokenSource : LinkedCancellationTokenSource
    {
        private MemoryOwner<CancellationTokenRegistration> registrations;

        internal MultipleLinkedCancellationTokenSource(ReadOnlySpan<CancellationToken> tokens, out bool isEmpty, CancellationToken first)
        {
            Debug.Assert(!tokens.IsEmpty);

            var writer = new BufferWriterSlim<CancellationTokenRegistration>(tokens.Length);
            try
            {
                foreach (var token in tokens)
                {
                    if (token != first && token.CanBeCanceled)
                    {
                        writer.Add(Attach(token));
                    }
                }

                if (first.CanBeCanceled && writer.WrittenCount > 0)
                {
                    writer.Add(Attach(first));
                }

                registrations = writer.DetachOrCopyBuffer();
                isEmpty = registrations.IsEmpty;
            }
            finally
            {
                writer.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (ref readonly var registration in registrations.Span)
                {
                    registration.Unregister();
                }
                
                registrations.Dispose();
            }
            
            base.Dispose(disposing);
        }
    }
}