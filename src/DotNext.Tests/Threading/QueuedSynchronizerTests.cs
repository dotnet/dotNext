namespace DotNext.Threading;

public sealed class QueuedSynchronizerTests : Test
{
    [Fact]
    public static async Task ThrowOnAcquisitionAsync()
    {
        await using var synchronizer = new MySynchronizer();
        await ThrowsAsync<ArithmeticException>(synchronizer.ThrowAsync().AsTask);
        False(synchronizer.TryAcquire());
    }
    
    private sealed class MySynchronizer : QueuedSynchronizer<bool>
    {
        public ValueTask ThrowAsync(CancellationToken token = default)
            => AcquireAsync(context: false, token);

        public bool TryAcquire() => TryAcquire(context: false);

        protected override bool CanAcquire(bool context) => context;

        protected override Exception GetAcquisitionException(bool canAcquire)
            => canAcquire ? null : new ArithmeticException();
    }
}