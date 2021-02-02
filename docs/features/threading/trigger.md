Asynchronous Trigger
====
[AsyncTrigger](xref:DotNext.Threading.AsyncTrigger) is the most general implementation of synchronization primitive compatible with asynchronous code. It can be used as a core for broad set of synchronization cases. The magic hides in ability to supply external state and predicate describing condition for resuming suspended callers. Each caller can supply its own condition for resuming and wait for signal. Signaling caller must supply the state that matches to one or more suspended callers. This concept is represented by the following methods:
* `Signal` method and its overloads can be used to signal about changes in external state which is also should be supplied as a parameter
* `WaitAsync` method and its overloads can be used to suspend the caller and specify predicate that describes condition for resuming. 
* `SignalAndWaitAsync` method is a combination of `Signal` and `WaitAsync` that works atomically. Useful in situations when you need to notify about state change and wait for another state.

As a result, this synchronization primitive can be used for building complex asynchronous state machines.