using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using Tasks;
    using Generic;

    public class AsyncBarrier : Disposable, IAsyncEvent
    {
        private long participants;
        private long currentPhase;
        private readonly AsyncCountdownEvent countdown;

        bool IAsyncEvent.IsSet => countdown.IsSet;

        bool ISynchronizer.HasWaiters => !countdown.IsSet;

        public AsyncBarrier(long participantCount)
        {
            participants = participantCount;
            countdown = new AsyncCountdownEvent(participants);
        }

        protected virtual void PostPhase(long phase)
        {

        }

        public long AddParticipants(long participantCount)
        {
            ThrowIfDisposed();
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
                if (countdown.TryAddCount(participantCount))
                {
                    participants.Add(participantCount);
                    return currentPhase;
                }
        }

        public long AddParticipant() => AddParticipants(1L);

        public void RemoveParticipants(long participantCount)
        {
            if (participantCount < 0L && participantCount > participants)
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            countdown.Signal(participantCount);
            participants.Add(-participantCount);
        }

        public void RemoveParticipant() => RemoveParticipants(1L);

        public Task<bool> SignalAndWait(TimeSpan timeout, CancellationToken token)
        {
            if (countdown.Signal(1L, true))
            {
                PostPhase(currentPhase.Add(1L));
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else
                return Wait(timeout, token);
        }

        bool IAsyncEvent.Reset() => countdown.Reset();

        bool IAsyncEvent.Signal() => countdown.Signal();

        public Task<bool> Wait(TimeSpan timeout, CancellationToken token) => countdown.Wait(timeout, token);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                countdown.Dispose();
        }
    }
}
