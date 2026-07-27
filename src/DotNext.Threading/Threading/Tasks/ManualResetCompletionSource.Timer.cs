using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static System.Threading.Timeout;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
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

        static ITimer CreateTimer(TimerCallback timerCallback,
            object? state,
            TimeSpan dueTime)
            => TimerQueueTimer.Create(timerCallback, state, dueTime, InfiniteTimeSpan, flowExecutionContext: false)
               ?? TimeProvider.System.CreateTimer(timerCallback, state, dueTime, InfiniteTimeSpan);
    }

    private static bool TryReset(ITimer timer, bool completedByTimeout)
    {
        return timer.Change(InfiniteTimeSpan, InfiniteTimeSpan) && TryResetCore(timer, completedByTimeout);

        static bool TryResetCore(ITimer timer, bool completedByTimeout)
        {
            ref var everQueued = ref TimerQueueTimer.IsEverQueued(timer);
            if (Unsafe.IsNullRef(in everQueued))
            {
                return false;
            }
            else if (!everQueued)
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
        }
    }
}

file static class TimerQueueTimer
{
    private const string TypeName = "System.Threading.TimerQueueTimer, System.Private.CoreLib";

    public static ITimer? Create(TimerCallback timerCallback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period,
        bool flowExecutionContext)
    {
        ITimer? result;
        try
        {
            result = CreateUnsafe(timerCallback, state, dueTime, period, flowExecutionContext) as ITimer;
        }
        catch (BadImageFormatException)
        {
            result = null;
        }

        return result;
    }
    
    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    [return: UnsafeAccessorType(TypeName)]
    private static extern object CreateUnsafe(TimerCallback timerCallback,
        object? state,
        TimeSpan dueTime,
        TimeSpan period,
        bool flowExecutionContext);

    public static ref bool IsEverQueued(ITimer timer)
    {
        ref var everQueued = ref Unsafe.NullRef<bool>();
        try
        {
            everQueued = ref IsEverQueuedUnsafe(timer);
        }
        catch (Exception e) when (e is BadImageFormatException or InvalidCastException)
        {
            // BadImageFormatException: the accessor failed to bind because the runtime internals drifted.
            // InvalidCastException: the timer is the TimeProvider.System fallback, not a TimerQueueTimer;
            // the UnsafeAccessorType parameter is type-checked (castclass) at the call.
        }

        return ref everQueued;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_everQueued")]
    private static extern ref bool IsEverQueuedUnsafe(
        [UnsafeAccessorType(TypeName)]
        object timer);
}