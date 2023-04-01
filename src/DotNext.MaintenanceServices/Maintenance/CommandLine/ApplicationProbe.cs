using System.Buffers;

namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;
using IMaintenanceConsole = IO.IMaintenanceConsole;

internal static class ApplicationProbe
{
    internal const string StartupProbeName = "startup";
    internal const string ReadinessProbeName = "readiness";
    internal const string LivenessProbeName = "liveness";

    internal static async Task InvokeProbeAsync(this IApplicationStatusProvider provider, string probeName, IMaintenanceConsole console, string successfulResponse, string unsuccessfulRespose, TimeSpan timeout, CancellationToken token)
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

        console.Out.Write(success ? successfulResponse : unsuccessfulRespose);
    }

    private static Task<bool> ExecuteProbeByNameAsync(IApplicationStatusProvider provider, string probeName, CancellationToken token) => probeName switch
    {
        LivenessProbeName => provider.LivenessProbeAsync(token),
        ReadinessProbeName => provider.ReadinessProbeAsync(token),
        StartupProbeName => provider.StartupProbeAsync(token),
        _ => Task.FromResult(true),
    };
}