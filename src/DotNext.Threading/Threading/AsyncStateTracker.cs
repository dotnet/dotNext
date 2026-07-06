using System.Diagnostics;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading;

/// <summary>
/// Represents tracker of the resource state.
/// </summary>
/// <remarks>
/// This class can be used to organize a stream of change notifications.
/// </remarks>
[DebuggerDisplay($"IsCompleted = {{{nameof(IsCompleted)}}}, Version = {{{nameof(CurrentState)}}}")]
public partial class AsyncStateTracker
{
    private readonly bool respectStaleCallers;
    private ulong currentVersion;
    private bool completed;

    /// <summary>
    /// Initializes a new tracker.
    /// </summary>
    public AsyncStateTracker()
    {
        syncRoot = new();
        pool = new();
    }
    
    /// <summary>
    /// Sets the expected number of concurrent flows.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not <see langword="null"/> and less than 1.</exception>
    public long ConcurrencyLevel
    {
        get => pool.MaximumRetained;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);

            pool = new(value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating the behavior of the suspended and stale callers.
    /// </summary>
    /// <remarks>
    /// Producer thread can call <see cref="TryAdvance()"/> and then immediately <see cref="TryComplete()"/>.
    /// Consumer thread has outdated <see cref="Token"/> and on the next call to <see cref="WaitNextAsync"/> method the behavior
    /// varies on this property. If it set to <see langword="true"/> then <see cref="WaitNextAsync"/> returns a new instance of <see cref="Token"/>
    /// because there is a new version of the state is available. Otherwise, <see cref="WaitNextAsync"/> returns <see langword="null"/>
    /// and the caller doesn't see the updated version.
    /// </remarks>
    public bool IsNewerTokenAvailableAfterCompletion
    {
        get => respectStaleCallers;
        init => respectStaleCallers = value;
    }

    /// <summary>
    /// Gets a value indicating that this tracker is completed.
    /// </summary>
    public bool IsCompleted => Volatile.Read(in completed);

    /// <summary>
    /// Gets the token that represents the current state.
    /// </summary>
    public Token CurrentState
    {
        get
        {
            var result = nuint.Size is sizeof(ulong)
                ? currentVersion
                : Unsafe.As<ulong, uint>(ref currentVersion);

            Volatile.ReadBarrier();
            return new(result);
        }
    }

    /// <summary>
    /// Waits for the state token next after <paramref name="stateToken"/>.
    /// </summary>
    /// <param name="stateToken">The token that represents the current version of the resource.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>
    /// <see langword="true"/> if the next resource state is observed;
    /// <see langword="false"/> if tracker is in completed state and no new states will be observed.
    /// </returns>
    public ValueTask<bool> WaitNextAsync(Token stateToken, CancellationToken token = default)
    {
        ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> factory;

        if (token.IsCancellationRequested)
        {
            factory = QueuedSynchronizer.CanceledFactory;
        }
        else
        {
            stateToken = stateToken.Next();
            lock (syncRoot)
            {
                if (stateToken.Version <= currentVersion)
                {
                    factory = GetFactory(respectStaleCallers || !completed);
                }
                else if (completed)
                {
                    factory = GetFactory(value: false);
                }
                else
                {
                    factory = EnqueueNode(stateToken.Version);
                }
            }
        }

        return factory.Invoke(token);

        static ISupplier<TimeSpan, CancellationToken, ValueTask<bool>> GetFactory(bool value)
            => value ? QueuedSynchronizer.TrueValueFactory : QueuedSynchronizer.FalseValueFactory;
    }

    /// <summary>
    /// Notifies suspended callers that the state has changed.
    /// </summary>
    /// <param name="resumed"><see langword="true"/> if at least one suspended caller is resumed; otherwise, <see langword="false"/>.</param>
    /// <returns>
    /// <see langword="true"/> if internal version of the resource is changed successfully;
    /// <see langword="false"/> if the tracker is completed.
    /// </returns>
    public bool TryAdvance(out bool resumed)
        => DrainWaitQueue<ResumeOldNodesStrategy>(out resumed);

    /// <summary>
    /// Notifies suspended callers that the state has changed.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if internal version of the resource is changed successfully;
    /// <see langword="false"/> if the tracker is completed.
    /// </returns>
    public bool TryAdvance() => TryAdvance(out _);

    /// <summary>
    /// Completes state change notifications so any subsequent calls to <see cref="WaitNextAsync"/> return immediately
    /// with <see langword="null"/>.
    /// </summary>
    /// <param name="resumed"><see langword="true"/> if at least one suspended caller is resumed; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the tracker is completed successfully; <see langword="false"/> if it's already completed.</returns>
    public bool TryComplete(out bool resumed)
        => DrainWaitQueue<CompletionStrategy>(out resumed);

    /// <summary>
    /// Completes state change notifications so any subsequent calls to <see cref="WaitNextAsync"/> return immediately
    /// with <see langword="null"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the tracker is completed successfully; <see langword="false"/> if it's already completed.</returns>
    public bool TryComplete() => TryComplete(out _);
}