namespace DotNext.Net.Multiplexing;

using Threading;

partial class Multiplexer<T>
{
    private TimeoutSource source = new(TimeProvider.System, token);

    protected void StartOperation(TimeSpan timeout) => source.TryStart(timeout);

    public CancellationToken RootToken => source.RootToken;

    protected CancellationToken TimeBoundedToken => source.Token;

    protected bool IsOperationCanceled(OperationCanceledException e)
        => e.CancellationToken == source.Token && source.IsCanceled;

    protected bool IsOperationTimedOut(OperationCanceledException e)
        => e.CancellationToken == source.Token && source.IsTimedOut;

    protected ValueTask ResetOperationTimeoutAsync()
        => source.TryReset() ? ValueTask.CompletedTask : RefreshTimeoutAsync();

    private async ValueTask RefreshTimeoutAsync()
    {
        var token = RootToken;
        await source.DisposeAsync().ConfigureAwait(false);
        source = new(TimeProvider.System, token);
    }
}