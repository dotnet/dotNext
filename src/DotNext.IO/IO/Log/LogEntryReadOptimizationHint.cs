namespace DotNext.IO.Log;

/// <summary>
/// Represents read hint that can help audit trail to optimize
/// read operations.
/// </summary>
public enum LogEntryReadOptimizationHint : byte
{
    /// <summary>
    /// Return log entry metadata and payload.
    /// </summary>
    None = 0,

    /// <summary>
    /// Return log entry metadata only.
    /// </summary>
    MetadataOnly = 1,
}