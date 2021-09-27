Asynchronous Trigger
====
.NEXT Threading Library offers a concept of asynchronous trigger for synchronization purposes. There are two types of triggers:
* Non-generic [AsyncTrigger](xref:DotNext.Threading.AsyncTrigger) for producer-consumer scenario in asynchronous code
* Generic [AsyncTrigger&lt;TState&gt;](xref:DotNext.Threading.AsyncTrigger`1) for state coordination scenario in asynchronous code.

# AsyncTrigger
[AsyncTrigger](xref:DotNext.Threading.AsyncTrigger) offers a simple synchronization for producer-consumer scenario:
* There is one or more suspended flows waiting for some event
* There is one or more signaling flows

The trigger offers the following methods for resuming and suspending the callers:
* `Signal` method allows to resume one or more suspended callers
* `WaitAsync` suspends the caller until the call of `Signal`
* `SignalAndWaitAsync` is an atomic combination of `Signal` and `WaitAsync` methods

Signaling method behaves in two ways:
* Resume all suspended callers
* Resume the first waiting caller in the queue

`AsyncTrigger` differs from [AsyncAutoResetEvent](xref:DotNext.Threading.AsyncAutoResetEvent) synchronization primitive in the following aspects:
* There is no _signaled_ state of the trigger. Therefore, any call to `WaitAsync` will be suspended and can be resumed only with `Signal` method
* The trigger can resume all suspended callers
* `SignalAndWaitAsync` allows to build a queue of signaling flows

# AsyncTrigger&lt;TState&gt;
[AsyncTrigger&lt;TState&gt;](xref:DotNext.Threading.AsyncTrigger`1) is the most general implementation of synchronization primitive compatible with asynchronous code. It can be used as a core for broad set of synchronization cases. The magic hides in ability to supply external state and predicate describing condition for resuming suspended callers. Each caller can supply its own condition for resuming and wait for signal. Signaling caller must supply the state that matches to one or more suspended callers. This concept is represented by the following methods:
* `Signal` method and its overloads can be used to signal about changes in the state
* `WaitAsync` method and its overloads can be used to suspend the caller and specify the condition for resuming and the transition of the state

As a result, this synchronization primitive can be used for building custom locks.

The following example demonstrates how to write a custom exclusive lock:
```csharp
using System.Runtime.CompilerServices;
using DotNext.Threading;

public sealed class MyExclusiveLock : AsyncTrigger<StrongBox<bool>>
{
    private sealed class LockManager : ITransition
    {
        internal static readonly LockManager Instance = new();

        // checks whether the lock can be acquired
        bool ITransition.Test(StrongBox<bool> state) => !state.Value;

        // turns the trigger into locked state
        void ITransition.Transit(StrongBox<bool> state) => state.Value = true;

        internal static void Release(StrongBox<bool> state) => state.Value = false;
    }

    public MyExclusiveLock()
        : base(new StrongBox<bool>(false))
    {
    }

    public ValueTask<bool> AcquireLockAsync(TimeSpan timeout, CancellationToken token)
      => WaitAsync(LockManager.Instance, timeout, token);

    public void ReleaseLock() => Signal(LockManager.Release);
}
```