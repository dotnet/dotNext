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

        public long CurrentPhaseNumber => currentPhase.VolatileRead();

        public long ParticipantCount => participants.VolatileRead();

        public long ParticipantsRemaining => countdown.CurrentCount;

        protected virtual void PostPhase(long phase)
        {

        }

        public long AddParticipants(long participantCount)
        {
            ThrowIfDisposed();
            countdown.TryAddCount(participantCount, true);  //always returns true if autoReset==true
            participants.Add(participantCount);
            return currentPhase;
        }

        public long AddParticipant() => AddParticipants(1L);

        public void RemoveParticipants(long participantCount)
        {
            if (participantCount < 0L || participantCount > ParticipantsRemaining)
                throw new ArgumentOutOfRangeException(nameof(participantCount));
            countdown.Signal(participantCount, false);
            participants.Add(-participantCount);
        }

        public void RemoveParticipant() => RemoveParticipants(1L);

        public Task<bool> SignalAndWait(TimeSpan timeout, CancellationToken token)
        {
            if(ParticipantCount == 0L)
                throw new InvalidOperationException();
            else if (countdown.Signal(1L, true))
            {
                PostPhase(currentPhase.Add(1L));
                return CompletedTask<bool, BooleanConst.True>.Task;
            }
            else
                return Wait(timeout, token);
        }

        public Task SignalAndWait(CancellationToken token) => SignalAndWait(TimeSpan.MaxValue, token);

        public Task<bool> SignalAndWait(TimeSpan timeout) => SignalAndWait(timeout, CancellationToken.None);

        public Task SignalAndWait() => SignalAndWait(TimeSpan.MaxValue);

        bool IAsyncEvent.Reset() => countdown.Reset();

        bool IAsyncEvent.Signal() => countdown.Signal();

        public Task<bool> Wait(TimeSpan timeout, CancellationToken token) => countdown.Wait(timeout, token);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                countdown.Dispose();
            participants = currentPhase = 0L;
        }
    }
}
