using System.Collections.Concurrent;
using DotNext.Diagnostics;

namespace DotNext.Threading.Leases;

[Collection(TestCollections.AdvancedSynchronization)]
public sealed class LeaseTests : Test
{
    [Fact]
    public static async Task InitialState()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        Equal(DefaultTimeout, provider.Storage.State.TimeToLive);

        var owner = Guid.NewGuid();
        Null(await provider.TryRenewAsync(owner, true, TestToken));
        Null(await provider.TryRenewAsync(owner, false, TestToken));
        Null(await provider.ReleaseAsync(owner, TestToken));

        var result = NotNull(await provider.TryAcquireAsync(owner, TestToken));
        True(result.State.Version > 0L);
        Equal(owner, result.State.OwnerId);
    }

    [Fact]
    public static async Task AcquireRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var owner = Guid.NewGuid();
        NotNull(await provider.TryAcquireAsync(owner, TestToken));
        NotNull(await provider.ReleaseAsync(owner, TestToken));

        await provider.AcquireAsync(owner, TestToken);
        Null(await provider.TryAcquireAsync(Guid.NewGuid(), TestToken));
        NotNull(await provider.UnsafeTryReleaseAsync(TestToken));
    }

    [Fact]
    public static async Task AcquisitionRenewsOwnedLease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var owner = Guid.NewGuid();

        // the response is lost
        var result = NotNull(await provider.TryAcquireAsync(owner, TestToken));

        var result2 = NotNull(await provider.TryAcquireAsync(owner, TestToken));
        True(result.State.Version < result2.State.Version);

        var result3 = await provider.AcquireAsync(owner, TestToken).AsTask().WaitAsync(DefaultTimeout, TestToken);
        True(result2.State.Version < result3.State.Version);

        Null(await provider.TryAcquireAsync(Guid.NewGuid(), TestToken));
    }

    [Fact]
    public static async Task RenewAfterRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var owner = Guid.NewGuid();
        await provider.AcquireAsync(owner, TestToken);
        await provider.UnsafeReleaseAsync(TestToken);
        Null(await provider.TryRenewAsync(owner, reacquire: false, TestToken));
        Null(await provider.TryRenewAsync(owner, reacquire: true, TestToken));
        Null(await provider.ReleaseAsync(owner, TestToken));
    }

    [Fact]
    public static async Task RetryRenewal()
    {
        using var provider1 = new TestLeaseProvider(DefaultTimeout);
        using var provider2 = new TestLeaseProvider(DefaultTimeout, provider1.Storage);
        var owner = Guid.NewGuid();

        NotNull(await provider1.TryAcquireAsync(owner, TestToken));

        // the response is lost
        NotNull(await provider1.TryRenewAsync(owner, reacquire: false, TestToken));
        NotNull(await provider1.TryRenewAsync(owner, reacquire: false, TestToken));

        // the provider that never observed the lease
        NotNull(await provider2.TryRenewAsync(owner, reacquire: false, TestToken));

        var anotherOwner = Guid.NewGuid();
        Null(await provider2.TryRenewAsync(anotherOwner, reacquire: true, TestToken));
        Null(await provider2.ReleaseAsync(anotherOwner, TestToken));
    }

    [Fact]
    public static async Task RenewAndReleaseExpiredLease()
    {
        var ttl = TimeSpan.FromMilliseconds(200);
        using var provider = new TestLeaseProvider(ttl);
        var owner = Guid.NewGuid();

        NotNull(await provider.TryAcquireAsync(owner, TestToken));
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(100), TestToken);

        Null(await provider.TryRenewAsync(owner, reacquire: false, TestToken));
        NotNull(await provider.TryRenewAsync(owner, reacquire: true, TestToken));

        await Task.Delay(ttl + TimeSpan.FromMilliseconds(100), TestToken);
        NotNull(await provider.ReleaseAsync(owner, TestToken));
        Null(await provider.ReleaseAsync(owner, TestToken));

        // released lease can be acquired immediately
        NotNull(await provider.TryAcquireAsync(Guid.NewGuid(), TestToken));
    }

    [Fact]
    public static async Task VersionContinuesFromStorageSequence()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);

        // the storage creates the lease with the version from the global sequence
        provider.Storage.State = new() { Version = 100L, CreatedAt = default, OwnerId = Guid.Empty, TimeToLive = DefaultTimeout };

        var owner = Guid.NewGuid();
        Equal(101L, NotNull(await provider.TryAcquireAsync(owner, TestToken)).State.Version);
        Equal(102L, NotNull(await provider.TryRenewAsync(owner, reacquire: false, TestToken)).State.Version);
        Equal(103L, await provider.ReleaseAsync(owner, TestToken));
    }

    [Fact]
    public static async Task EmptyOwner()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);

        await ThrowsAsync<ArgumentException>(provider.TryAcquireAsync(Guid.Empty, TestToken).AsTask);
        await ThrowsAsync<ArgumentException>(provider.AcquireAsync(Guid.Empty, TestToken).AsTask);
        await ThrowsAsync<ArgumentException>(provider.TryRenewAsync(Guid.Empty, reacquire: true, TestToken).AsTask);
        await ThrowsAsync<ArgumentException>(provider.ReleaseAsync(Guid.Empty, TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.TryAcquireAsync<int>(Guid.NewGuid(), 0, null, TestToken).AsTask);
    }

    [Fact]
    public static async Task FightForLease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);

        var acquisition1 = Task.Run(async () => await provider.TryAcquireAsync(Guid.NewGuid(), TestToken), TestToken);
        var acquisition2 = Task.Run(async () => await provider.TryAcquireAsync(Guid.NewGuid(), TestToken), TestToken);

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
        Equal(1L, consumer.Version);
        False(consumer.Token.IsCancellationRequested);
        False(consumer.Expiration.IsExpired);

        await consumer.Token.WaitAsync();
        await provider.UnsafeReleaseAsync(TestToken);

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
            await provider.AcquireAsync(Guid.NewGuid(), TestToken);
            task = provider.AcquireAsync(Guid.NewGuid(), TestToken).AsTask();
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
        await provider.AcquireAsync(Guid.NewGuid(), TestToken);
        await Task.Delay(TimeSpan.FromSeconds(2.5), TestToken);

        // the lease expires in ~0.5 sec, so the acquisition must not wait for the elapsed time (~2.5 sec)
        var ts = new Timestamp();
        await provider.AcquireAsync(Guid.NewGuid(), TestToken);
        True(ts.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public static async Task AcquireWithCreationTimeInFuture()
    {
        var ttl = TimeSpan.FromMilliseconds(100);
        using var provider = new TestLeaseProvider(ttl);
        provider.Storage.State = new()
        {
            TimeToLive = ttl,
            Version = 1L,
            CreatedAt = DateTimeOffset.UtcNow + TimeSpan.FromHours(1), // clock skew
            OwnerId = Guid.NewGuid(),
        };

        // the expiration doesn't depend on the wall clock
        await provider.AcquireAsync(Guid.NewGuid(), TestToken).AsTask().WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task NewProviderWaitsForUnreleasedLease()
    {
        var ttl = TimeSpan.FromSeconds(1);
        using var provider = new TestLeaseProvider(ttl);
        provider.Storage.State = new()
        {
            TimeToLive = ttl,
            Version = 1L,
            CreatedAt = DateTimeOffset.UtcNow - TimeSpan.FromHours(1), // expired according to the wall clock
            OwnerId = Guid.NewGuid(),
        };

        var owner = Guid.NewGuid();
        Null(await provider.TryAcquireAsync(owner, TestToken));

        // timer resolution is coarser than the monotonic clock used by the provider
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(100), TestToken);
        NotNull(await provider.TryAcquireAsync(owner, TestToken));
    }

    [Fact]
    public static async Task ObservationAfterInstantiation()
    {
        var ttl = TimeSpan.FromSeconds(1);
        var abandoned = Guid.NewGuid();
        using var provider = new TestLeaseProvider(ttl);
        provider.Storage.State = new()
        {
            TimeToLive = ttl,
            Version = 1L,
            CreatedAt = DateTimeOffset.UtcNow,
            OwnerId = abandoned,
        };

        True(await provider.ObserveAsync(TestToken));

        // the lease is tracked since the observation, not since the first acquisition attempt
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(100), TestToken);
        False(await provider.ObserveAsync(TestToken));

        var owner = Guid.NewGuid();
        NotNull(await provider.TryAcquireAsync(owner, TestToken));
        True(await provider.ObserveAsync(TestToken));

        NotNull(await provider.ReleaseAsync(owner, TestToken));
        False(await provider.ObserveAsync(TestToken));
    }

    [Fact]
    public static async Task ObserveEmptyStorage()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        False(await provider.ObserveAsync(TestToken));
    }

    [Fact]
    public static async Task ClockSkewBetweenProviders()
    {
        var ttl = TimeSpan.FromSeconds(2);
        using var provider1 = new TestLeaseProvider(ttl);
        using var provider2 = new TestLeaseProvider(ttl, provider1.Storage, new SkewedTimeProvider(TimeSpan.FromHours(1)));
        Guid owner1 = Guid.NewGuid(), owner2 = Guid.NewGuid();

        NotNull(await provider1.TryAcquireAsync(owner1, TestToken));

        // the wall clock of the second provider is ahead, but the lease is still owned
        Null(await provider2.TryAcquireAsync(owner2, TestToken));
        Null(await provider2.TryAcquireAsync(owner2, TestToken));

        // renewal resets the observation of the second provider
        await Task.Delay(ttl / 2, TestToken);
        NotNull(await provider1.TryRenewAsync(owner1, reacquire: false, TestToken));
        await Task.Delay(ttl / 2 + TimeSpan.FromMilliseconds(200), TestToken);
        Null(await provider2.TryAcquireAsync(owner2, TestToken));

        // released lease can be acquired immediately
        NotNull(await provider1.ReleaseAsync(owner1, TestToken));
        NotNull(await provider2.TryAcquireAsync(owner2, TestToken));
    }

    [Fact]
    public static async Task ProviderTakesOverAbandonedLease()
    {
        var ttl = TimeSpan.FromMilliseconds(500);
        using var provider1 = new TestLeaseProvider(ttl);
        using var provider2 = new TestLeaseProvider(ttl, provider1.Storage);
        Guid owner1 = Guid.NewGuid(), owner2 = Guid.NewGuid();

        NotNull(await provider1.TryAcquireAsync(owner1, TestToken));
        Null(await provider2.TryAcquireAsync(owner2, TestToken));

        await provider2.AcquireAsync(owner2, TestToken).AsTask().WaitAsync(DefaultTimeout, TestToken);
        Null(await provider1.TryAcquireAsync(owner1, TestToken));
        Null(await provider1.TryRenewAsync(owner1, reacquire: true, TestToken));
    }

    [Fact]
    public static async Task ExpirationIncludesUpdaterDuration()
    {
        var ttl = TimeSpan.FromSeconds(5);
        using var provider = new TestLeaseProvider(ttl);
        var result = NotNull(await provider.TryAcquireAsync(Guid.NewGuid(), 0, SlowUpdater, TestToken));

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
    public static async Task WorkerNotStartedWithoutLease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        await using var consumer = new TestLeaseConsumer(provider);

        var started = false;
        await ThrowsAsync<TimeoutException>(() => consumer.ExecuteAsync(Worker, TestToken));
        False(started);

        Task<int> Worker(CancellationToken token)
        {
            started = true;
            return Task.FromResult(42);
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

    [Fact]
    public static async Task KeyedLeasesAreIndependent()
    {
        using var provider = new TestKeyedLeaseProvider(DefaultTimeout);
        Guid owner1 = Guid.NewGuid(), owner2 = Guid.NewGuid();

        NotNull(await provider.TryAcquireAsync("a", owner1, TestToken));
        NotNull(await provider.TryAcquireAsync("b", owner2, TestToken));
        Null(await provider.TryAcquireAsync("a", owner2, TestToken));
        Null(await provider.TryRenewAsync("b", owner1, reacquire: true, TestToken));

        NotNull(await provider.ReleaseAsync("a", owner1, TestToken));
        NotNull(await provider.TryAcquireAsync("a", owner2, TestToken));
        NotNull(await provider.TryRenewAsync("b", owner2, reacquire: false, TestToken));
    }

    [Fact]
    public static async Task KeyedLeaseExpiration()
    {
        var ttl = TimeSpan.FromMilliseconds(200);
        using var provider1 = new TestKeyedLeaseProvider(ttl);
        using var provider2 = new TestKeyedLeaseProvider(ttl, provider1.Storages);
        Guid owner1 = Guid.NewGuid(), owner2 = Guid.NewGuid();

        NotNull(await provider1.TryAcquireAsync("a", owner1, TestToken));
        True(await provider2.ObserveAsync("a", TestToken));
        False(await provider2.ObserveAsync("b", TestToken));
        Null(await provider2.TryAcquireAsync("a", owner2, TestToken));

        // timer resolution is coarser than the monotonic clock used by the provider
        await Task.Delay(ttl + TimeSpan.FromMilliseconds(100), TestToken);
        False(await provider2.ObserveAsync("a", TestToken));
        NotNull(await provider2.TryAcquireAsync("a", owner2, TestToken));
        Null(await provider1.TryRenewAsync("a", owner1, reacquire: true, TestToken));
    }

    [Fact]
    public static async Task KeyedUnsafeRelease()
    {
        using var provider = new TestKeyedLeaseProvider(DefaultTimeout);
        Guid owner1 = Guid.NewGuid(), owner2 = Guid.NewGuid();

        Equal(1L, (await provider.AcquireAsync("a", owner1, TestToken)).State.Version);
        Equal(2L, await provider.UnsafeReleaseAsync("a", TestToken));

        // released lease can be acquired immediately
        Equal(3L, NotNull(await provider.TryAcquireAsync("a", owner2, TestToken)).State.Version);
        Equal(4L, await provider.UnsafeTryReleaseAsync("a", TestToken));
        Null(await provider.ReleaseAsync("a", owner2, TestToken));
    }

    [Fact]
    public static async Task KeyedDisposeCancelsAcquisition()
    {
        var provider = new TestKeyedLeaseProvider(DefaultTimeout);
        await provider.AcquireAsync("a", Guid.NewGuid(), TestToken);

        var acquisition = provider.AcquireAsync("a", Guid.NewGuid(), TestToken).AsTask();
        False(acquisition.IsCompleted);

        provider.Dispose();
        await ThrowsAsync<ObjectDisposedException>(() => acquisition);
    }

    [Fact]
    public static async Task KeyedInvalidArguments()
    {
        using var provider = new TestKeyedLeaseProvider(DefaultTimeout);

        await ThrowsAsync<ArgumentNullException>(provider.TryAcquireAsync(null, Guid.NewGuid(), TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.AcquireAsync(null, Guid.NewGuid(), TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.TryRenewAsync(null, Guid.NewGuid(), reacquire: true, TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.ReleaseAsync(null, Guid.NewGuid(), TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.UnsafeTryReleaseAsync(null, TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.UnsafeReleaseAsync(null, TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.ObserveAsync(null, TestToken).AsTask);
        await ThrowsAsync<ArgumentException>(provider.TryAcquireAsync("a", Guid.Empty, TestToken).AsTask);
        await ThrowsAsync<ArgumentNullException>(provider.TryAcquireAsync("a", Guid.NewGuid(), 0, null!, TestToken).AsTask);
    }

    private sealed class SkewedTimeProvider(TimeSpan offset) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + offset;
    }

    private sealed class LeaseStorage(TimeSpan ttl)
    {
        private readonly System.Threading.Lock syncRoot = new();

        // the storage creates the lease with the configured time-to-live
        private LeaseState<int> state = new()
        {
            Version = 0L,
            CreatedAt = default,
            OwnerId = Guid.Empty,
            TimeToLive = ttl,
        };

        internal LeaseState<int> State
        {
            get
            {
                lock (syncRoot)
                {
                    return state;
                }
            }

            set
            {
                lock (syncRoot)
                {
                    state = value;
                }
            }
        }

        internal bool TryUpdate(in LeaseState<int> newState)
        {
            lock (syncRoot)
            {
                if (state.Version + 1L == newState.Version)
                {
                    state = newState;
                    return true;
                }
            }

            return false;
        }
    }

    private sealed class TestLeaseProvider(TimeSpan ttl, LeaseStorage storage = null, TimeProvider provider = null) : LeaseProvider<int>(provider)
    {
        internal LeaseStorage Storage { get; } = storage ?? new(ttl);

        internal CancellationToken Token => LifetimeToken;

        protected override async ValueTask<LeaseState<int>> GetStateAsync(CancellationToken token)
        {
            await Task.Yield();
            return Storage.State;
        }

        protected override async ValueTask<bool> TryUpdateStateAsync(LeaseState<int> state, CancellationToken token)
        {
            await Task.Yield();
            return Storage.TryUpdate(in state);
        }
    }

    private sealed class TestKeyedLeaseProvider(TimeSpan ttl, ConcurrentDictionary<string, LeaseStorage> storages = null) : LeaseProvider<string, int>
    {
        internal ConcurrentDictionary<string, LeaseStorage> Storages { get; } = storages ?? new();

        private LeaseStorage GetStorage(string leaseId) => Storages.GetOrAdd(leaseId, _ => new(ttl));

        protected override async ValueTask<LeaseState<int>> GetStateAsync(string leaseId, CancellationToken token)
        {
            await Task.Yield();
            return GetStorage(leaseId).State;
        }

        protected override async ValueTask<bool> TryUpdateStateAsync(string leaseId, LeaseState<int> state, CancellationToken token)
        {
            await Task.Yield();
            return GetStorage(leaseId).TryUpdate(in state);
        }
    }

    private sealed class TestLeaseConsumer(TestLeaseProvider provider) : LeaseConsumer
    {
        private readonly Guid owner = Guid.NewGuid();

        protected override async ValueTask<AcquisitionResult?> TryAcquireCoreAsync(CancellationToken token = default)
        {
            return await provider.TryAcquireAsync(owner, token) is { } result
                ? new AcquisitionResult { Version = result.State.Version, TimeToLive = result.Expiration.Value }
                : null;
        }

        protected override async ValueTask<AcquisitionResult?> TryRenewCoreAsync(CancellationToken token)
        {
            return await provider.TryRenewAsync(owner, reacquire: false, token) is { } result
                ? new AcquisitionResult { Version = result.State.Version, TimeToLive = result.Expiration.Value }
                : null;
        }

        protected override ValueTask<long?> ReleaseCoreAsync(CancellationToken token)
            => provider.ReleaseAsync(owner, token);

        protected override ValueTask DisposeAsyncCore()
        {
            provider.Dispose();
            return base.DisposeAsyncCore();
        }
    }
}