Dynamic Buffers
====
[ArrayBufferWriter&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.buffers.arraybufferwriter-1) represents default implementation of dynamically-sized, heap-based and array-backed buffer. Unfortunately, it's not flexible enough in the following aspects:
* Not possible to use array or memory pooling mechanism. As a result, umnanaged memory cannot be used for such writer.
* Not compatible with [ArraySegment&lt;T&gt;](https://docs.microsoft.com/en-us/dotnet/api/system.arraysegment-1)
* No easy way to obtain stream over written memory except copying
* Allocation on the heap

