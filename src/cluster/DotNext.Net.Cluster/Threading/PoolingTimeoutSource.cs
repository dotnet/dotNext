namespace DotNext.Threading;

internal sealed class PoolingTimeoutSource(CancellationToken token) : IAsyncDisposable, IDisposable
{
    private TimeoutSource source = new(TimeProvider.System, token);

    public void Start(TimeSpan timeout) => source.TryStart(timeout);

    public CancellationToken Token => source.Token;

    public bool IsCanceled(OperationCanceledException e)
        => e.CancellationToken == source.Token && source.IsCanceled;

    public bool IsTimedOut(OperationCanceledException e)
        => e.CancellationToken == source.Token && source.IsTimedOut;

    public ValueTask ResetAsync(CancellationToken token)
        => source.TryReset() ? ValueTask.CompletedTask : RefreshAsync(token);

    private async ValueTask RefreshAsync(CancellationToken token)
    {
        await source.DisposeAsync().ConfigureAwait(false);
        source = new(TimeProvider.System, token);
    }

    public ValueTask DisposeAsync() => source.DisposeAsync();

    public void Dispose() => source.Dispose();
}