Barrier
====
[AsyncBarrier](../../api/DotNext.Threading.AsyncBarrier.yml) is an asynchronous alternative of [Barrier](https://docs.microsoft.com/en-us/dotnet/api/system.threading.barrier) with some additional features:
* Post-phase action is asynchronous
* It is possible to wait for phase completion without signaling (use `Wait` method instead of `SignalAndWait`)



