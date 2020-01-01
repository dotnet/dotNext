using System;
using System.Threading;

namespace DotNext.Threading
{
    internal readonly struct TimeoutTokenSource : IDisposable
    {
        private readonly CancellationTokenSource? timeoutSource;
        private readonly CancellationTokenSource? linkedSource;
        internal readonly CancellationToken Token;

        internal TimeoutTokenSource(Timeout timeout, CancellationToken token)
        {
            if(timeout.IsInfinite)
            {
                timeoutSource = linkedSource = null;
                Token = token;
            }
            else
            {
                timeoutSource = new CancellationTokenSource(timeout);
                linkedSource = token.CanBeCanceled ? CancellationTokenSource.CreateLinkedTokenSource(token, timeoutSource.Token) : timeoutSource;
                Token = linkedSource.Token;
            }
        }
        
        public void Dispose()
        {
            linkedSource?.Dispose();
            timeoutSource?.Dispose();
        }
    }
}