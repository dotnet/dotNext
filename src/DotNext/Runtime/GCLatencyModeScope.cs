using System.Runtime;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

/// <summary>
/// Represents lexical scope of the specific GC latency mode.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct GCLatencyModeScope : IDisposable
{
    private readonly GCLatencyMode currentMode;

    /// <summary>
    /// Initializes a new scope that affects GC intrusion level.
    /// </summary>
    /// <param name="mode">GC latency mode.</param>
    public GCLatencyModeScope(GCLatencyMode mode)
    {
        currentMode = GCSettings.LatencyMode;
        GCSettings.LatencyMode = mode;
    }

    /// <summary>
    /// Cancels previously defined GC latency.
    /// </summary>
    public void Dispose() => GCSettings.LatencyMode = currentMode;

    /// <summary>
    /// Creates a scope with <see cref="GCLatencyMode.SustainedLowLatency"/> GC intrusion level.
    /// </summary>
    public static GCLatencyModeScope SustainedLowLatency => new(GCLatencyMode.SustainedLowLatency);

    /// <summary>
    /// Creates a scope with <see cref="GCLatencyMode.LowLatency"/> GC intrusion level.
    /// </summary>
    public static GCLatencyModeScope LowLatency => new(GCLatencyMode.LowLatency);
}