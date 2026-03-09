using System.Runtime;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

/// <summary>
/// Represents lexical scope of the specific GC latency mode.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct GCLatencyModeScope : IDisposable
{
    private readonly GCLatencyMode? currentMode;

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
    public void Dispose()
    {
        if (currentMode.HasValue)
            GCSettings.LatencyMode = currentMode.GetValueOrDefault();
    }
}

/// <summary>
/// Represents extension for <see cref="GCLatencyMode"/> type.
/// </summary>
public static class GCLatencyModeExtensions
{
    /// <summary>
    /// Enters the specified GC latency mode.
    /// </summary>
    /// <param name="mode">The desired mode.</param>
    /// <returns>The scope that controls GC latency mode.</returns>
    public static GCLatencyModeScope Enable(this GCLatencyMode mode) => new(mode);
}