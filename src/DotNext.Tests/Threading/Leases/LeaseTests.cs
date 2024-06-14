
namespace DotNext.Threading.Leases;

public sealed class LeaseTests : Test
{
    [Fact]
    public static async Task AcquireOrRenewInitialState()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        Null(await provider.TryRenewAsync(default, true));
        Null(await provider.TryRenewAsync(default, false));
        Null(await provider.ReleaseAsync(default));

        var result = NotNull(await provider.TryAcquireOrRenewAsync(default));
        True(result.State.Identity >> default(LeaseIdentity));
    }

    [Fact]
    public static async Task Acquisition()
    {
        using var provider = new TestLeaseProvider(DefaultTimeout);
        NotNull(await provider.TryAcquireAsync());
    }

    [Fact]
    public static void Preceding()
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

    private sealed class TestLeaseProvider(TimeSpan ttl) : LeaseProvider<int>(ttl)
    {
        private readonly AsyncReaderWriterLock syncRoot = new();
        private State currentState;

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
}