using System.Threading;

namespace DotNext.Threading
{
    /// <summary>
    /// Represents helper methods for working with linked cancellation tokens.
    /// </summary>
    public static class LinkedTokenSourceFactory
    {
        /// <summary>
        /// Links two cancellation token.
        /// </summary>
        /// <param name="first">The first cancellation token. Can be modified by this method.</param>
        /// <param name="second">The second cancellation token.</param>
        /// <returns>The linked token source; or <see langword="null"/> if <paramref name="first"/> or <paramref name="second"/> is not cancelable.</returns>
        public static CancellationTokenSource? LinkTo(this ref CancellationToken first, CancellationToken second)
        {
            var result = default(CancellationTokenSource);
            if (first == second)
            {
                // nothing to do, just return from this method
            }
            else if (!first.CanBeCanceled)
            {
                first = second;
            }
            else if (second.CanBeCanceled)
            {
                result = CancellationTokenSource.CreateLinkedTokenSource(first, second);
                first = result.Token;
            }

            return result;
        }
    }
}