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
    /// <param name="captureContext"><see langword="true"/> to execute the callback within the captured context; otherwise, <see langword="false"/>.</param>
    /// <returns>The object that can be used to cancel the registration.</returns>
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
}