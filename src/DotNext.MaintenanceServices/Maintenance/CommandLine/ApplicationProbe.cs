namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;

internal static class ApplicationProbe
{
    internal const string StartupProbeName = "startup";
    internal const string ReadinessProbeName = "readiness";
    internal const string LivenessProbeName = "liveness";

    internal static async Task InvokeProbeAsync(this IApplicationStatusProvider provider, string probeName, string successfulResponse,
        string unsuccessfulRespose, TimeSpan timeout, TextWriter writer, CancellationToken token)
    {
        bool success;

        if (timeout != Timeout.InfiniteTimeSpan)
        {
            var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                timeoutSource.CancelAfter(timeout);
                success = await ExecuteProbeByNameAsync(provider, probeName, timeoutSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException e) when (!token.IsCancellationRequested)
            {
                // timeout occurred
                throw new TimeoutException(CommandResources.CommandTimeoutOccurred, e);
            }
            finally
            {
                timeoutSource.Dispose();
            }
        }
        else
        {
            success = await ExecuteProbeByNameAsync(provider, probeName, token).ConfigureAwait(false);
        }

        await writer.WriteAsync((success ? successfulResponse : unsuccessfulRespose).AsMemory(), token).ConfigureAwait(false);
    }

    private static Task<bool> ExecuteProbeByNameAsync(IApplicationStatusProvider provider, string probeName, CancellationToken token) => probeName switch
    {
        LivenessProbeName => provider.LivenessProbeAsync(token),
        ReadinessProbeName => provider.ReadinessProbeAsync(token),
        StartupProbeName => provider.StartupProbeAsync(token),
        _ => Task.FromResult(true),
    };
}