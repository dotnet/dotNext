Barrier
====
[AsyncBarrier](../../api/DotNext.Threading.AsyncBarrier.yml) is an asynchronous alternative of [Barrier](https://docs.microsoft.com/en-us/dotnet/api/system.threading.barrier) with some additional features:
* Post-phase action is asynchronous
* It is possible to wait for phase completion without signaling (use `Wait` method instead of `SignalAndWait`)
* It is possible to signal without waiting for phase completion

Last two features are possible because barrier implements [IAsyncEvent](../../api/DotNext.Threading.IAsyncEvent.yml) interface which is common to all event-based synchronization primitives in .NEXT Threading library.



