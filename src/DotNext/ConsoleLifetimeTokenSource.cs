using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext;

/// <summary>
/// Represents a token that becomes canceled if the console application is requested to stop.
/// </summary>
[UnsupportedOSPlatform("android")]
[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
public sealed class ConsoleLifetimeTokenSource : CancellationTokenSource
{
    private readonly IReadOnlyCollection<PosixSignalRegistration> registrations;

    /// <summary>
    /// Initializes a new lifetime token source.
    /// </summary>
    public ConsoleLifetimeTokenSource()
    {
        ReadOnlySpan<PosixSignal> signals = [PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM];
        var result = new PosixSignalRegistration[signals.Length];
        Action<PosixSignalContext> handler = Cancel;
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = PosixSignalRegistration.Create(signals[i], handler);
        }

        registrations = result;
    }

    private void Cancel(PosixSignalContext context)
    {
        context.Cancel = true;
        try
        {
            Cancel(throwOnFirstException: false);
        }
        catch (ObjectDisposedException)
        {
            context.Cancel = false;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposable.Dispose(registrations);
        }

        base.Dispose(disposing);
    }
}