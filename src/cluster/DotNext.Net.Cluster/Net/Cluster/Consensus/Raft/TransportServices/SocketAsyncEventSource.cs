using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices
{
    internal class SocketAsyncEventSource : SocketAsyncEventArgs, IValueTaskSource
    {
        private ManualResetValueTaskSourceCore<bool> taskSource;

        internal SocketAsyncEventSource(bool runContinuationsAsynchronously)
            => taskSource = new ManualResetValueTaskSourceCore<bool> { RunContinuationsAsynchronously = runContinuationsAsynchronously };

        void IValueTaskSource.GetResult(short token)
            => taskSource.GetResult(token);

        ValueTaskSourceStatus IValueTaskSource.GetStatus(short token)
            => taskSource.GetStatus(token);

        void IValueTaskSource.OnCompleted(Action<object?> continuation, object? state, short token, ValueTaskSourceOnCompletedFlags flags)
            => taskSource.OnCompleted(continuation, state, token, flags);

        internal virtual void Reset() => taskSource.Reset();

        internal ValueTask Task => new ValueTask(this, taskSource.Version);

        private protected virtual bool IsCancellationRequested(out CancellationToken token)
        {
            token = default;
            return false;
        }

        protected override void OnCompleted(SocketAsyncEventArgs e)
        {
            switch (e.SocketError)
            {
                case SocketError.Success:
                    taskSource.SetResult(true);
                    break;
                case (SocketError.OperationAborted or SocketError.ConnectionAborted) when IsCancellationRequested(out var token):
                    taskSource.SetException(new OperationCanceledException(token));
                    break;
                default:
                    taskSource.SetException(new SocketException((int)e.SocketError));
                    break;
            }
        }
    }
}