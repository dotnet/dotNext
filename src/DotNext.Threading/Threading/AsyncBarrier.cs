using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using Tasks;
    using Generic;

    public class AsyncBarrier : Synchronizer, IAsyncEvent
    {
        private long participants;
        private long currentPhase;
        private long remaining;
        private Dictionary<long, CancelableTaskCompletionSource<bool>> phaseHandlers;

        public AsyncBarrier(long participantCount)
        {
            participants = participantCount;
        }

        protected virtual void PostPhase()
        {
            if (!(phaseHandlers is null) && phaseHandlers.TryGetValue(currentPhase, out var source))
                source.TrySetResult(true);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public long AddParticipants(long participantCount)
        {
            if (participantCount < 0)
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            ThrowIfDisposed();
            participants += participantCount;
            remaining += participantCount;
            if (node is null)
                node = new WaitNode();
            return currentPhase;
        }

        public long AddParticipant() => AddParticipants(1L);

        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Reset()
        {
            ThrowIfDisposed();
            if (node is null || node.TrySetCanceled())
            {
                remaining = participants;
                node = new WaitNode();
                return true;
            }
            else
                return false;
        }


        private bool Signal()
        {
            ThrowIfDisposed();
            switch (remaining)
            {
                case 0L:
                    throw new InvalidOperationException();
                case 1L:
                    remaining = 0;
                    node?.Complete();
                    PostPhase();
                    remaining = participants;
                    node = new WaitNode();
                    return true;
                default:
                    remaining -= 1L;
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        bool IAsyncEvent.Signal() => Signal();

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> SignalAndWait(TimeSpan timeout, CancellationToken token)
            => Signal() ? CompletedTask<bool, BooleanConst.True>.Task : Wait(timeout, token);

        

        [MethodImpl(MethodImplOptions.Synchronized)]
        public Task<bool> WaitForPhase(long phaseNumber, TimeSpan timeout, CancellationToken token)
        {
            ThrowIfDisposed();
            if (phaseNumber <= currentPhase)
                return CompletedTask<bool, BooleanConst.True>.Task;
            
        }
    }
}
