using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
    [SuppressMessage("Performance", "CA1823", Justification = "False positive")]
    private const string TimerQueueTimerType = "System.Threading.TimerQueueTimer, System.Private.CoreLib";
    
    // written inside the Completing window; read and cleared at reset time
    private bool completedByTimeout;
    
    // With CancellationTokenSource.TryReset() it's not possible to reuse CTS if it's canceled.
    // For timeout-based async operations, timeouts can happen from time to time, which causes
    // allocation of the CTS every time when the completion source is reused. We want reuse the timer
    // even if it's fired.
    private ITimer? timeoutTracker;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Arm([NotNull] ref ITimer? timer, TimerCallback callback, object? state, TimeSpan timeout)
    {
        if (timer is null)
        {
            timer = CreateTimer(callback, state, timeout);
        }
        else
        {
            timer.Change(timeout, InfiniteTimeSpan);
        }
    }
    
    private static bool TryReset(ITimer timer, bool completedByTimeout)
        => timer.Change(InfiniteTimeSpan, InfiniteTimeSpan) && TryResetCore(timer, completedByTimeout);

    private static bool TryResetCore(ITimer timer, bool completedByTimeout)
    {
        ref var everQueued = ref Unsafe.NullRef<bool>();
        try
        {
            everQueued = ref IsEverQueued(timer);
        }
        catch (Exception e) when (e is BadImageFormatException or InvalidCastException)
        {
            // BadImageFormatException: the accessor failed to bind because the runtime internals drifted.
            // InvalidCastException: the timer is the TimeProvider.System fallback, not a TimerQueueTimer;
            // the UnsafeAccessorType parameter is type-checked (castclass) at the call.
            return false;
        }

        if (!everQueued)
        {
            // the timer never fired, nothing to do
        }
        else if (completedByTimeout)
        {
            // The fired callback is our own completed timeout: it unboxed the version at entry,
            // causally before the completion/consumption that led here, so it can never observe
            // the reused version box again. Safe to reuse even if the callback is still unwinding.
            everQueued = false;
        }
        else
        {
            // The timer fired, but the task was completed by something else: the callback may still
            // be queued or in-flight and hasn't read the version box yet. Reusing the box would let
            // the stale callback observe the fresh version and time out the next task spuriously.
            return false;
        }

        return true;
            
        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_everQueued")]
        static extern ref bool IsEverQueued(
            [UnsafeAccessorType(TimerQueueTimerType)]
            object timer);
    }
    
    private static ITimer CreateTimer(TimerCallback timerCallback,
        object? state,
        TimeSpan dueTime)
    {
        ITimer? result;
        try
        {
            result = Create(timerCallback, state, dueTime, InfiniteTimeSpan, flowExecutionContext: false) as ITimer;
        }
        catch (BadImageFormatException)
        {
            result = null;
        }

        return result ?? TimeProvider.System.CreateTimer(timerCallback, state, dueTime, InfiniteTimeSpan);

        [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
        [return: UnsafeAccessorType(TimerQueueTimerType)]
        static extern object Create(TimerCallback timerCallback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period,
            bool flowExecutionContext);
    }
}