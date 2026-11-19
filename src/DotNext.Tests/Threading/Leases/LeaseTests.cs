using DotNext.Diagnostics;

namespace DotNext.Threading.Leases;

[Collection(TestCollections.AdvancedSynchronization)]
public sealed class LeaseTests : Test
{
    [Fact]
    public static async Task AcquireOrRenewInitialState()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        Equal(DefaultTimeout, provider.TimeToLive);
        Null(await provider.TryRenewAsync(default, true, TestToken));
        Null(await provider.TryRenewAsync(default, false, TestToken));
        Null(await provider.ReleaseAsync(default, TestToken));

        var result = NotNull(await provider.TryAcquireOrRenewAsync(default, TestToken));
        True(result.State.Identity >> default(LeaseIdentity));
    }

    [Fact]
    public static async Task AcquireRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = NotNull(await provider.TryAcquireAsync(TestToken));
        NotNull(await provider.ReleaseAsync(result.State.Identity, TestToken));

        await provider.AcquireAsync(TestToken);
        Null(await provider.TryAcquireAsync(TestToken));
        NotNull(await provider.UnsafeTryReleaseAsync(TestToken));
    }

    [Fact]
    public static async Task RenewOrAcquire()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = NotNull(await provider.TryAcquireOrRenewAsync(default, TestToken));
        var result2 = NotNull(await provider.TryAcquireOrRenewAsync(result.State.Identity, TestToken));
        True(result.State.Identity << result2.State.Identity);
    }

    [Fact]
    public static async Task RenewAfterRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = await provider.AcquireAsync(TestToken);
        await provider.UnsafeReleaseAsync(TestToken);
        Null(await provider.TryRenewAsync(result.State.Identity, reacquire: false, TestToken));
    }

    [Fact]
    public static void Precedence()
    {
        True(default(LeaseIdentity) << new LeaseIdentity { Version = 1UL });
        False(default(LeaseIdentity) >> new LeaseIdentity { Version = 1UL });
        False(default(LeaseIdentity) << new LeaseIdentity { Version = 2UL });
    }

    [Fact]
    public static async Task FightForLease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);

        var acquisition1 = Task.Run(async () => await provider.TryAcquireAsync(TestToken), TestToken);
        var acquisition2 = Task.Run(async () => await provider.TryAcquireAsync(TestToken), TestToken);

        var tasks = await Task.WhenAll(acquisition1, acquisition2);

        True(tasks is [null, not null] or [not null, null]);
    }

    [Fact]
    public static async Task FightForLeaseUsingConsumer()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        await using var consumer1 = new TestLeaseConsumer(provider);
        await using var consumer2 = new TestLeaseConsumer(provider);

        var acquisition1 = Task.Run(async () => await consumer1.TryAcquireAsync(TestToken), TestToken);
        var acquisition2 = Task.Run(async () => await consumer2.TryAcquireAsync(TestToken), TestToken);

        var tasks = await Task.WhenAll(acquisition1, acquisition2);

        True(tasks is [false, true] or [true, false]);
    }

    [Fact]
    public static async Task ConsumerTokenState()
    {
        using var provider = new TestLeaseProvider(TimeSpan.FromMilliseconds(100));
        await using var consumer = new TestLeaseConsumer(provider);
        True(consumer.Token.IsCancellationRequested);
        True(consumer.Expiration.IsExpired);

        True(await consumer.TryAcquireAsync(TestToken));
        Equal(1UL, consumer.LeaseId.Version);
        False(consumer.Token.IsCancellationRequested);
        False(consumer.Expiration.IsExpired);

        await consumer.Token.WaitAsync();
        await Task.Delay(provider.TimeToLive, TestToken);

        False(await consumer.ReleaseAsync(TestToken));
    }

    [Fact]
    public static async Task ConsumerRenew()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        await using var consumer = new TestLeaseConsumer(provider) { ClockDriftBound = 1 };
        True(await consumer.TryAcquireAsync(TestToken));
        var expected = consumer.Token;

        True(await consumer.TryRenewAsync(TestToken));
        Equal(consumer.Token, expected);
    }

    [Fact]
    public static async Task AcquireUsingConsumer()
    {
        var pause = TimeSpan.FromMilliseconds(100);
        using var provider = new TestLeaseProvider(pause);
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync(TestToken));
        await consumer.AcquireAsync(pause, Random.Shared, TestToken);
    }

    [Fact]
    public static async Task WorkerProtectedWithLease()
    {
        var pause = TimeSpan.FromMilliseconds(500);
        using var provider = new TestLeaseProvider(pause);
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync(TestToken));

        var result = await InvokeAsThread(() => consumer.ExecuteAsync(Worker));
        Equal(42, result);

        static async Task<int> Worker(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1_000), token);
            return 42;
        }
    }

    [Fact]
    public static async Task WorkerLeaseExpired()
    {
        using var provider = new TestLeaseProvider(TimeSpan.FromMilliseconds(100));
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync(TestToken));
        await provider.UnsafeReleaseAsync(TestToken);

        await ThrowsAsync<TimeoutException>(() => consumer.ExecuteAsync(Worker, TestToken));

        static async Task<int> Worker(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250_000), token);
            return 42;
        }
    }

    [Fact]
    public static async Task DisposeConcurrently()
    {
        Task task;
        CancellationToken token;
        using (var provider = new TestLeaseProvider(DefaultTimeout))
        {
            await provider.AcquireAsync(TestToken);
            task = provider.AcquireAsync(TestToken).AsTask();
            False(task.IsCompleted);
            token = provider.Token;
        }

        await ThrowsAnyAsync<ObjectDisposedException>(task);
        True(token.IsCancellationRequested);
    }

    [Fact]
    public static async Task AcquireWaitsForRemainingTime()
    {
        var ttl = TimeSpan.FromSeconds(3);
        using var provider = new TestLeaseProvider(ttl);
        await provider.AcquireAsync(TestToken);
        await Task.Delay(TimeSpan.FromSeconds(2.5), TestToken);

        // the lease expires in ~0.5 sec, so the acquisition must not wait for the elapsed time (~2.5 sec)
        var ts = new Timestamp();
        await provider.AcquireAsync(TestToken);
        True(ts.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public static async Task AcquireWithCreationTimeInFuture()
    {
        using var provider = new TestLeaseProvider(TimeSpan.FromMilliseconds(100));
        provider.CurrentState = new()
        {
            Identity = new() { Version = 1UL },
            CreatedAt = DateTimeOffset.UtcNow + TimeSpan.FromHours(1), // clock skew
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken);
        cts.CancelAfter(TimeSpan.FromMilliseconds(500));
        await ThrowsAnyAsync<OperationCanceledException>(provider.AcquireAsync(cts.Token).AsTask);
    }

    [Fact]
    public static async Task ExpirationIncludesUpdaterDuration()
    {
        var ttl = TimeSpan.FromSeconds(5);
        using var provider = new TestLeaseProvider(ttl);
        var result = NotNull(await provider.TryAcquireAsync(0, SlowUpdater, TestToken));

        True(result.Expiration.TryGetRemainingTime(out var reported));
        var actual = ttl - (DateTimeOffset.UtcNow - result.State.CreatedAt);
        True(reported <= actual + TimeSpan.FromMilliseconds(50), $"Reported: {reported}, actual: {actual}");

        static async ValueTask<int> SlowUpdater(int arg, int metadata, CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), token);
            return metadata + 1;
        }
    }

    [Fact]
    public static async Task WorkerStartedLate()
    {
        using var provider = new TestLeaseProvider(TimeSpan.FromSeconds(2));
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync(TestToken));

        // 70% of the lease is consumed, renewal must be scheduled using the remaining time
        await Task.Delay(TimeSpan.FromSeconds(1.4), TestToken);
        Equal(42, await consumer.ExecuteAsync(Worker, TestToken));

        static async Task<int> Worker(CancellationToken token)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);
            return 42;
        }
    }

    [Fact]
    public static async Task DisposeCancelsLeaseToken()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync(TestToken));

        var token = consumer.Token;
        False(token.IsCancellationRequested);

        await consumer.DisposeAsync();
        True(token.IsCancellationRequested);
        True(consumer.Token.IsCancellationRequested);
    }

    private sealed class TestLeaseProvider(TimeSpan ttl) : LeaseProvider<int>(ttl)
    {
        private readonly AsyncReaderWriterLock syncRoot = new();
        private State currentState;

        internal CancellationToken Token => LifetimeToken;

        internal State CurrentState
        {
            set => currentState = value;
        }

        protected override async ValueTask<State> GetStateAsync(CancellationToken token)
        {
            await syncRoot.EnterReadLockAsync(token).ConfigureAwait(false);
            try
            {
                return currentState;
            }
            finally
            {
                syncRoot.Release();
            }
        }

        protected override async ValueTask<bool> TryUpdateStateAsync(State state, CancellationToken token)
        {
            bool result;
            await syncRoot.EnterWriteLockAsync(token).ConfigureAwait(false);
            try
            {
                if (result = currentState.Identity << state.Identity)
                {
                    currentState = state;
                }
            }
            finally
            {
                syncRoot.Release();
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                syncRoot.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class TestLeaseConsumer(TestLeaseProvider provider) : LeaseConsumer
    {
        protected override async ValueTask<AcquisitionResult?> TryAcquireCoreAsync(CancellationToken token = default)
        {
            return await provider.TryAcquireAsync(token) is { } result
                ? new AcquisitionResult { Identity = result.State.Identity, TimeToLive = result.Expiration.Value }
                : null;
        }

        protected override async ValueTask<AcquisitionResult?> TryRenewCoreAsync(LeaseIdentity identity,
            CancellationToken token)
        {
            return await provider.TryRenewAsync(identity, reacquire: false, token) is { } result
                ? new AcquisitionResult { Identity = result.State.Identity, TimeToLive = result.Expiration.Value }
                : null;
        }

        protected override ValueTask<LeaseIdentity?> ReleaseCoreAsync(LeaseIdentity identity,
            CancellationToken token)
            => provider.ReleaseAsync(identity, token);

        protected override ValueTask DisposeAsyncCore()
        {
            provider.Dispose();
            return base.DisposeAsyncCore();
        }
    }
}