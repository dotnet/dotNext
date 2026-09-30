# Rules

- **Release in `finally`.** `await AcquireAsync(...)` / `Enter*LockAsync(...)` must be followed by
  `try { ... } finally { Release(); }`. The scope returned by `AsyncLock.AcquireLockAsync` must be disposed with `using`.
- **The locks are not reentrant.** Acquiring a lock you already hold asynchronously waits forever.
  Don't call a method that takes the lock from code that already holds it. Split into a public
  method that locks and a private method that assumes the lock is held.
- **Timeouts: two families of overloads.** `TryXxxAsync(timeout, token)` returns `false` on timeout.
  `XxxAsync(timeout, token)` throws `TimeoutException`. Use `Try*` when timeout is an expected outcome.
- **Always flow a `CancellationToken`.** Cancellation throws `OperationCanceledException` and does not acquire the lock.
- **Never block on the returned `ValueTask`.** No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()`.
  Never await the same `ValueTask` twice or store it for later. Await it once, immediately.
- **Don't protect the same state with two different primitives.** If some code paths use the
  `AsyncExclusiveLock` and others use `lock`, the state is unprotected. If some callers must stay synchronous,
  use the same lock's synchronous API (see "Mixed sync and async callers").
- **Name clash:** `DotNext.Threading.Timeout` conflicts with `System.Threading.Timeout` when both namespaces are
  imported (including via implicit usings). Write `System.Threading.Timeout.InfiniteTimeSpan` or add an alias.
- **Dispose the primitive when its owner is disposed.** `Dispose()` interrupts suspended callers
  (they get `ObjectDisposedException`). `DisposeAsync()` disposes gracefully. Don't dispose a
  primitive that other components still use.
- **Back-pressure is opt-in.** Set `ConcurrencyLevel` and `HasConcurrencyLimit = true` to make callers get
  `ConcurrencyLimitReachedException` instead of queueing without limit.
- Prefer a dedicated primitive field (`AsyncExclusiveLock`, `AsyncReaderWriterLock`) over
  `AsyncLock.AcquireLockAsync(obj)` in new code. It is explicit, can be disposed, and has no hidden per-object state.