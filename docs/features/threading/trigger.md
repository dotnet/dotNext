Asynchronous Trigger
====
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