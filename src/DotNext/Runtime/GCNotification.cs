using static System.Threading.Timeout;

namespace DotNext.Runtime;

/// <summary>
/// Provides a way to receive notifications from Garbage Collector asynchronously.
/// </summary>
public abstract partial class GCNotification
{
    /// <summary>
    /// Registers a callback to be executed asynchronously on approach of GC notification.
    /// </summary>
    /// <remarks>
    /// The suspended caller will be resumed after approach of actual GC notification.
    /// However, the delay between these two events is possible.
    /// </remarks>
    /// <typeparam name="T">The type of the state to be passed to the callback.</typeparam>
    /// <param name="callback">The callback to be executed asynchronously.</param>
    /// <param name="state">The object to be passed to the callback.</param>
    /// <param name="captureContext"><see langword="true"/> to execute the callback within the captured synchronization context; otherwise, <see langword="false"/>.</param>
    /// <returns>The object that can be used to cancel the registration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callback"/> is <see langword="null"/>.</exception>
    public Registration Register<T>(Action<T, GCMemoryInfo> callback, T state, bool captureContext = false)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return new(new Tracker<T>(this, state, callback, captureContext));
    }

    /// <summary>
    /// Waits for GC notification asynchronously.
    /// </summary>
    /// <remarks>
    /// The result of this method must be awaited.
    /// The suspended caller will be resumed after approach of actual GC notification.
    /// However, the delay between these two events is possible.
    /// </remarks>
    /// <param name="timeout">The time to wait for the notification.</param>
    /// <param name="token">The token that can be used to cancel the notification.</param>
    /// <returns>The information about last occurred GC.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="TimeoutException">The notification did not arrive in timely manner.</exception>
    public ValueTask<GCMemoryInfo> WaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        Task<GCMemoryInfo> result;

        if (token.IsCancellationRequested)
        {
            result = Task.FromCanceled<GCMemoryInfo>(token);
        }
        else
        {
            var tracker = new Tracker(this);
            result = tracker.Task.WaitAsync(timeout, token);

            // attach callback for cleanup
            result.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(new GCIntermediateReference(tracker).Clear);
            GC.KeepAlive(tracker);
        }

        return new(result);
    }

    /// <summary>
    /// Waits for GC notification asynchronously.
    /// </summary>
    /// <remarks>
    /// The result of this method must be awaited.
    /// The suspended caller will be resumed after approach of actual GC notification.
    /// However, the delay between these two events is possible.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the notification.</param>
    /// <returns>The information about last occurred GC.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public ValueTask<GCMemoryInfo> WaitAsync(CancellationToken token = default)
        => WaitAsync(InfiniteTimeSpan, token);

    /// <summary>
    /// Creates a filter that allows to detect heap compaction.
    /// </summary>
    /// <returns>A new filter.</returns>
    public static GCNotification HeapCompaction()
        => HeapCompactionFilter.Instance;

    /// <summary>
    /// Creates a filter that triggers notification on every GC occurred.
    /// </summary>
    /// <returns>A new filter.</returns>
    public static GCNotification GCTriggered()
        => GCEvent.Instance;

    /// <summary>
    /// Creates a filter that allows to detect garbage collection of the specified generation.
    /// </summary>
    /// <param name="generation">The expected generation.</param>
    /// <returns>A new filter.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="generation"/> is less than 0 or greater than <see cref="GC.MaxGeneration"/>.</exception>
    public static GCNotification GCTriggered(int generation)
    {
        if (generation < 0 || generation > GC.MaxGeneration)
            throw new ArgumentOutOfRangeException(nameof(generation));

        return new GenerationFilter(generation);
    }

    /// <summary>
    /// Creates a filter that allows to detect managed heap fragmentation threshold.
    /// </summary>
    /// <param name="threshold">The memory threshold. The memory threshold; must be in range (0, 1].</param>
    /// <returns>A new filter.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="threshold"/> is invalid.</exception>
    public static GCNotification HeapFragmentation(double threshold)
    {
        if (!double.IsFinite(threshold) || !threshold.IsBetween(0D, 1D, BoundType.RightClosed))
            throw new ArgumentOutOfRangeException(nameof(threshold));

        return new HeapFragmentationThresholdFilter(threshold);
    }
}