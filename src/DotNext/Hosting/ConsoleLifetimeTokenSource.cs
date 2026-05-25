using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace DotNext.Hosting;

/// <summary>
/// Represents a token that becomes canceled if the console application is requested to stop.
/// </summary>
public sealed class ConsoleLifetimeTokenSource : CancellationTokenSource
{
    private readonly IReadOnlyCollection<PosixSignalRegistration> registrations;

    /// <summary>
    /// Initializes a new lifetime token source.
    /// </summary>
    [UnsupportedOSPlatform("android")]
    [UnsupportedOSPlatform("browser")]
    [UnsupportedOSPlatform("ios")]
    [UnsupportedOSPlatform("tvos")]
    public ConsoleLifetimeTokenSource()
    {
        ReadOnlySpan<PosixSignal> signals = [PosixSignal.SIGINT, PosixSignal.SIGQUIT, PosixSignal.SIGTERM];
        var result = new PosixSignalRegistration[signals.Length];
        Action<PosixSignalContext> handler = Cancel;
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = PosixSignalRegistration.Create(signals[i], handler);
        }

        registrations = result;
    }
    
    /// <summary>
    /// Gets a value indicating that <see cref="ConsoleLifetimeTokenSource"/> is supported.
    /// </summary>
    [UnsupportedOSPlatformGuard("android")]
    [UnsupportedOSPlatformGuard("browser")]
    [UnsupportedOSPlatformGuard("ios")]
    [UnsupportedOSPlatformGuard("tvos")]
    public static bool IsSupported
        => !OperatingSystem.IsTvOS() && !OperatingSystem.IsIOS() && !OperatingSystem.IsAndroid() && !OperatingSystem.IsBrowser();

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