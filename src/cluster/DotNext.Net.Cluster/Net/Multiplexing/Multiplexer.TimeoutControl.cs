using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Multiplexing;

using Threading;

partial class Multiplexer
{
    private TimeoutSource source;

    public required CancellationToken RootToken
    {
        get => source.RootToken;

        [MemberNotNull(nameof(source))] init => source = new(TimeProvider.System, value);
    }

    protected void StartOperation(TimeSpan timeout) => source.TryStart(timeout);

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