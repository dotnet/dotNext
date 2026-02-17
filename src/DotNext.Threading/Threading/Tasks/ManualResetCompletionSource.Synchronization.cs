using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

partial class ManualResetCompletionSource
{
    // There are two possible concurrent flows:
    // 1. activation => subscription
    // 2. completion => consumption
    // For instance, the completion can happen simultaneously with activation. But subscription cannot happen simultaneously
    // with activation. The completion can be initiated concurrently.
    private const uint ActivatingState = 0B_0000_0100;
    private const uint ActivatedState = 0B_0000_1100; // this state guarantees that timeout/token trackers are set

    private const uint SubscribingState = 0B_0001_1100;
    private const uint SubscribedState = 0B_0011_1100; // this state guarantees that the continuation is set

    private const uint CompletingState = 0B_0000_0001;
    private const uint CompletedState = 0B_0000_0011; // this state guarantees that the result is set

    // sync flag between subscription thread and completion thread. The thread that set this flag first,
    // is able to invoke the continuation if the source is completed
    private const uint NotificationLockState = 0B_0100_0000;
    private const uint ConsumedState = 0B_1000_0000;
    
    // Lower 16 bits are reserved for state transitions
    // Upper 16 bits are reserved for the version
    private const uint VersionMask = (uint)ushort.MaxValue << 16;
    private uint syncState;

    internal short CurrentVersion => GetVersion(syncState);

    private short ResetCore()
    {
        short newVersion;
        for (uint stateCopy = syncState, tmp;; stateCopy = tmp)
        {
            // do not reset if completion, subscription, or activation is in progress
            if ((stateCopy & CompletedState) is CompletingState
                || (stateCopy & ActivatedState) is ActivatingState
                || (stateCopy & SubscribedState) is SubscribingState)
            {
                tmp = Volatile.Read(in syncState);
                continue;
            }

            newVersion = short.CreateTruncating(GetVersion(stateCopy) + 1);
            tmp = Interlocked.CompareExchange(ref syncState, (uint)(ushort)newVersion << 16, stateCopy);

            if (tmp == stateCopy)
                break;
        }

        return newVersion;
    }

    // Activation can happen simultaneously with the completion process.
    // However, if pending completion is detected, we don't want to proceed with the activation
    private bool BeginActivation(out short currentVersion)
    {
        var stateCopy = syncState;
        var expectedToken = currentVersion = GetVersion(stateCopy);
        var expectedState = stateCopy & VersionMask;

        var tmp = Interlocked.CompareExchange(ref syncState, expectedState | ActivatingState, expectedState);
        if (tmp == expectedState)
            return true;

        string message;
        if (GetVersion(tmp) != expectedToken)
        {
            message = ExceptionMessages.InvalidSourceToken;
        }
        else if ((tmp & ~VersionMask) is CompletingState or CompletedState)
        {
            return false;
        }
        else
        {
            // attempt to activate concurrently
            message = ExceptionMessages.InvalidSourceState;
        }

        throw new InvalidOperationException(message);
    }

    private void EndActivation() => Interlocked.Or(ref syncState, ActivatedState);

    private bool BeginCompletion()
    {
        var stateCopy = syncState;
        return BeginCompletion(stateCopy, GetVersion(stateCopy));
    }

    private bool BeginCompletion(short expectedToken)
        => BeginCompletion(OverrideToken(syncState, expectedToken), expectedToken);

    private bool BeginCompletion(uint stateCopy, short expectedToken)
    {
        for (uint tmp;; stateCopy = OverrideToken(tmp, expectedToken))
        {
            var expectedState = stateCopy & ~CompletedState;

            tmp = Interlocked.CompareExchange(ref syncState, expectedState | CompletingState, expectedState);
            if (tmp == expectedState)
                break;

            // compare tokens and check the state to avoid situation when two threads trying
            // to enter the Completing state
            if (GetVersion(tmp) != expectedToken || (tmp & CompletedState) is CompletedState)
                return false;
        }

        return true;
    }

    private bool EndCompletion()
        => TryAcquireNotificationLock(Interlocked.Or(ref syncState, CompletedState) | CompletedState);

    private bool BeginSubscription(short expectedToken)
    {
        for (uint stateCopy = syncState, tmp;; stateCopy = tmp)
        {
            var expectedState = stateCopy & (VersionMask | ActivatedState);

            tmp = Interlocked.CompareExchange(ref syncState, expectedState | SubscribingState, expectedState);
            if (tmp == expectedState)
                break;

            if (GetVersion(tmp) != expectedToken)
                throw new InvalidOperationException(ExceptionMessages.InvalidSourceToken);

            if ((tmp & CompletedState) is CompletedState)
                return false;
        }

        return true;
    }

    private bool EndSubscription()
        => TryAcquireNotificationLock(Interlocked.Or(ref syncState, SubscribedState) | SubscribedState);
    
    // If it's subscribed and completed, we can try to acquire the notification lock
    private bool TryAcquireNotificationLock(uint stateCopy)
        => (stateCopy & ~VersionMask) is (SubscribedState | CompletedState)
           && Interlocked.CompareExchange(ref syncState, stateCopy | NotificationLockState, stateCopy) == stateCopy;

    private protected void Consume(short expectedToken)
    {
        for (uint stateCopy = OverrideToken(syncState, expectedToken), tmp;; stateCopy = OverrideToken(tmp, expectedToken))
        {
            var expectedState = stateCopy & ~ConsumedState;
            tmp = Interlocked.CompareExchange(ref syncState, expectedState | ConsumedState, expectedState);

            if (tmp == expectedState)
                break;

            string message;
            if ((tmp & ConsumedState) is not 0)
            {
                message = ExceptionMessages.InvalidSourceState;
            }
            else if (GetVersion(tmp) != expectedToken)
            {
                message = ExceptionMessages.InvalidSourceToken;
            }
            else
            {
                continue;
            }

            throw new InvalidOperationException(message);
        }

        AfterConsumed();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint OverrideToken(uint state, short expectedToken)
        => ((uint)(ushort)expectedToken << 16) | (state & ushort.MaxValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static short GetVersion(uint state)
        => (short)(state >> 16);
}