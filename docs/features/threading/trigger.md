Asynchronous Trigger
====
[AsyncTrigger](xref:DotNext.Threading.AsyncTrigger) is the most general implementation of synchronization primitive compatible with asynchronous code. It can be used as a core for broad set of synchronization cases. The magic hides in ability to supply external state and predicate describing condition for resuming suspended callers. Each caller can supply its own condition for resuming and wait for signal. Signaling caller must supply the state that matches to one or more suspended callers. This concept is represented by the following methods:
* `Signal` method and its overloads can be used to signal about changes in external state which is also should be supplied as a parameter
* `WaitAsync` method and its overloads can be used to suspend the caller and specify predicate that describes condition for resuming. 
* `SignalAndWaitAsync` method is a combination of `Signal` and `WaitAsync` that works atomically. Useful in situations when you need to notify about state change and wait for another state.

As a result, this synchronization primitive can be used for building complex asynchronous state machines.

Signaling method behaves in two ways:
* Resume all suspended callers in unordered fashion. For instance, if you have the queue with configiration _A1-A2-A3_ and _A1_, _A2_ return **true** then they will be removed from the queue and _A2_ remains untouched.
* Fair dequeuing when suspended callers resumed in strict order. In this case only _A1_ will be removed. _A2_ and _A3_ remain untouched because _A2_ returns **false**.

Fairness policy for dequeuing can be passed as **bool** parameter to `Signal` and `SignalAndWaitAsync` methods.

You can check [this](https://github.com/dotnet/dotNext/blob/master/src/cluster/DotNext.Net.Cluster/Net/Cluster/Consensus/Raft/PersistentState.LockManager.cs) class as an example of complex asynchronous lock constructed on top of `AsyncTrigger`. The implementation is used by persistent Write-Ahead Log to allow multiple writers co-exist without blocking in some specific cirtumstances.