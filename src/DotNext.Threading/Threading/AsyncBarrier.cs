using static System.Threading.Timeout;

namespace DotNext.Threading
{
    /// <summary>
    /// Enables multiple tasks to cooperatively work on an algorithm in parallel through multiple phases.
    /// </summary>
    /// <remarks>
    /// This is asynchronous version of <see cref="Barrier"/> with small differences:
    /// <list type="bullet">
    /// <item><description>Post-phase action is presented by virtual method <see cref="PostPhase(long)"/>.</description></item>
    /// <item><description>It it possible to wait for phase completion without signal.</description></item>
    /// <item><description>It is possible to signal without waiting of phase completion.</description></item>
    /// <item><description>Post-phase action is asynchronous.</description></item>
    /// <item><description>Number of phases is limited by <see cref="long"/> data type.</description></item>
    /// </list>
    /// </remarks>
    public class AsyncBarrier : Disposable, IAsyncEvent
    {
        private readonly AsyncCountdownEvent countdown;
        private long participants;
        private long currentPhase;

        /// <summary>
        /// Initializes a new Barrier withe given number of participating tasks.
        /// </summary>
        /// <param name="participantCount">The number of participating tasks.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="participantCount"/> is less than 0.</exception>
        public AsyncBarrier(long participantCount)
        {
            if (participantCount < 0)
                throw new ArgumentOutOfRangeException(nameof(participantCount));

            participants = participantCount;
            countdown = new AsyncCountdownEvent(participants);
        }

        /// <inheritdoc/>
        bool IAsyncEvent.IsSet => countdown.IsSet;

        /// <summary>
        /// Gets the number of the barrier's current phase.
        /// </summary>
        public long CurrentPhaseNumber => currentPhase.VolatileRead();

        /// <summary>
        /// Gets the total number of participants in the barrier.
        /// </summary>
        public long ParticipantCount => participants.VolatileRead();

        /// <summary>
        /// Gets the number of participants in the barrier that haven't yet signaled in the current phase.
        /// </summary>
        public long ParticipantsRemaining => countdown.CurrentCount;

        /// <summary>
        /// The action to be executed after each phase.
        /// </summary>
        /// <param name="phase">The current phase number.</param>
        /// <returns>A task representing post-phase asynchronous execution.</returns>
        protected virtual ValueTask PostPhase(long phase) => ValueTask.CompletedTask;

        /// <summary>
        /// Notifies this barrier that there will be additional participants.
        /// </summary>
        /// <param name="participantCount">The number of additional participants to add to the barrier.</param>
        /// <returns>The phase number of the barrier in which the new participants will first participate.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="participantCount"/> is less than 0.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public long AddParticipants(long participantCount)
        {
            ThrowIfDisposed();
            countdown.TryAddCount(participantCount, true);  // always returns true if autoReset==true
            participants.Add(participantCount);
            return currentPhase;
        }

        /// <summary>
        /// Notifies this barrier that there will be additional participant.
        /// </summary>
        /// <returns>The phase number of the barrier in which the new participants will first participate.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public long AddParticipant() => AddParticipants(1L);

        /// <summary>
        /// Notifies this barrier that there will be fewer participants.
        /// </summary>
        /// <remarks>
        /// This method may resume all tasks suspended by <see cref="WaitAsync(TimeSpan, CancellationToken)"/>
        /// and <see cref="SignalAndWaitAsync(TimeSpan, CancellationToken)"/> methods.
        /// </remarks>
        /// <param name="participantCount">The number of additional participants to remove from the barrier.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="participantCount"/> less than 1 or greater that <see cref="ParticipantsRemaining"/>.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void RemoveParticipants(long participantCount)
        {
            if (participantCount > ParticipantsRemaining)
                throw new ArgumentOutOfRangeException(nameof(participantCount));

            countdown.Signal(participantCount);
            participants.Add(-participantCount);
        }

        /// <summary>
        /// Notifies this barrier that there will be one less participant.
        /// </summary>
        /// <remarks>
        /// This method may resume all tasks suspended by <see cref="WaitAsync(TimeSpan, CancellationToken)"/>
        /// and <see cref="SignalAndWaitAsync(TimeSpan, CancellationToken)"/> methods.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void RemoveParticipant() => RemoveParticipants(1L);

        /// <summary>
        /// Signals that a participant has reached the barrier and waits
        /// for all other participants to reach the barrier as well.
        /// </summary>
        /// <param name="timeout">The time to wait for phase completion.</param>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns><see langword="true"/> if all other participants reached the barrier; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public async ValueTask<bool> SignalAndWaitAsync(TimeSpan timeout, CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (ParticipantCount == 0L)
                throw new InvalidOperationException();

            if (await countdown.SignalAndWaitAsync(timeout, token))
            {
                await PostPhase(currentPhase.Add(1L));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Signals that a participant has reached the barrier and waits
        /// for all other participants to reach the barrier as well.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns>The task representing waiting operation.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public async ValueTask SignalAndWaitAsync(CancellationToken token = default)
        {
            ThrowIfDisposed();

            if (ParticipantCount == 0L)
                throw new InvalidOperationException();

            await countdown.SignalAndWaitAsync(token);
            await PostPhase(currentPhase.Add(1L));
        }

        /// <inheritdoc/>
        bool IAsyncEvent.Reset() => countdown.Reset();

        /// <inheritdoc/>
        bool IAsyncEvent.Pulse() => countdown.Signal();

        /// <summary>
        /// Waits for all other participants to reach the barrier.
        /// </summary>
        /// <param name="timeout">The time to wait for phase completion.</param>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns><see langword="true"/> if all other participants reached the barrier; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask<bool> WaitAsync(TimeSpan timeout, CancellationToken token = default)
            => countdown.WaitAsync(timeout, token);

        /// <summary>
        /// Waits for all other participants to reach the barrier.
        /// </summary>
        /// <param name="token">The token that can be used to cancel the waiting operation.</param>
        /// <returns>The task representing asynchronous result.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public ValueTask WaitAsync(CancellationToken token = default)
            => countdown.WaitAsync(token);

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                countdown.Dispose();

            participants = currentPhase = 0L;
            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override ValueTask DisposeAsyncCore() => countdown.DisposeAsync();
    }
}
