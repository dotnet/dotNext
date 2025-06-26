
namespace DotNext.Threading.Leases;

[Collection(TestCollections.AdvancedSynchronization)]
public sealed class LeaseTests : Test
{
    [Fact]
    public static async Task AcquireOrRenewInitialState()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        Equal(DefaultTimeout, provider.TimeToLive);
        Null(await provider.TryRenewAsync(default, true));
        Null(await provider.TryRenewAsync(default, false));
        Null(await provider.ReleaseAsync(default));

        var result = NotNull(await provider.TryAcquireOrRenewAsync(default));
        True(result.State.Identity >> default(LeaseIdentity));
    }

    [Fact]
    public static async Task AcquireRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = NotNull(await provider.TryAcquireAsync());
        NotNull(await provider.ReleaseAsync(result.State.Identity));

        await provider.AcquireAsync();
        Null(await provider.TryAcquireAsync());
        NotNull(await provider.UnsafeTryReleaseAsync());
    }

    [Fact]
    public static async Task RenewOrAcquire()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = NotNull(await provider.TryAcquireOrRenewAsync(default));
        var result2 = NotNull(await provider.TryAcquireOrRenewAsync(result.State.Identity));
        True(result.State.Identity << result2.State.Identity);
    }

    [Fact]
    public static async Task RenewAfterRelease()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        var result = await provider.AcquireAsync();
        await provider.UnsafeReleaseAsync();
        Null(await provider.TryRenewAsync(result.State.Identity, reacquire: false));
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

        var acquisition1 = Task.Run(async () => await provider.TryAcquireAsync());
        var acquisition2 = Task.Run(async () => await provider.TryAcquireAsync());

        var tasks = await Task.WhenAll(acquisition1, acquisition2);

        True(tasks is [null, not null] or [not null, null]);
    }

    [Fact]
    public static async Task FightForLeaseUsingConsumer()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        await using var consumer1 = new TestLeaseConsumer(provider);
        await using var consumer2 = new TestLeaseConsumer(provider);

        var acquisition1 = Task.Run(async () => await consumer1.TryAcquireAsync());
        var acquisition2 = Task.Run(async () => await consumer1.TryAcquireAsync());

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

        True(await consumer.TryAcquireAsync());
        Equal(1UL, consumer.LeaseId.Version);
        False(consumer.Token.IsCancellationRequested);
        False(consumer.Expiration.IsExpired);

        await consumer.Token.WaitAsync();
        await Task.Delay(provider.TimeToLive);
        
        False(await consumer.ReleaseAsync());
    }

    [Fact]
    public static async Task ConsumerRenew()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        await using var consumer = new TestLeaseConsumer(provider) { ClockDriftBound = 1 };
        True(await consumer.TryAcquireAsync());
        var expected = consumer.Token;

        True(await consumer.TryRenewAsync());
        Equal(consumer.Token, expected);
    }

    [Fact]
    public static async Task AcquireUsingConsumer()
    {
        var pause = TimeSpan.FromMilliseconds(100);
        using var provider = new TestLeaseProvider(pause);
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync());
        await consumer.AcquireAsync(pause, Random.Shared);
    }
    
    [Fact]
    public static async Task WorkerProtectedWithLease()
    {
        var pause = TimeSpan.FromMilliseconds(500);
        using var provider = new TestLeaseProvider(pause);
        await using var consumer = new TestLeaseConsumer(provider);
        True(await consumer.TryAcquireAsync());

        var result = await SuspendContext(() => consumer.ExecuteAsync(Worker));
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
        True(await consumer.TryAcquireAsync());
        await provider.UnsafeReleaseAsync();

        await ThrowsAsync<TimeoutException>(() => consumer.ExecuteAsync(Worker));

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
            await provider.AcquireAsync();
            task = provider.AcquireAsync().AsTask();
            False(task.IsCompleted);
            token = provider.Token;
        }

        await ThrowsAsync<ObjectDisposedException>(Func.Constant(task));
        True(token.IsCancellationRequested);
    }

    private sealed class TestLeaseProvider(TimeSpan ttl) : LeaseProvider<int>(ttl)
    {
        private readonly AsyncReaderWriterLock syncRoot = new();
        private State currentState;

        internal CancellationToken Token => LifetimeToken;

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