namespace DotNext.IO.Log;

/// <summary>
/// Represents log entry in the audit trail.
/// </summary>
public interface ILogEntry : IDataTransferObject
{
    /// <summary>
    /// Gets a value indicating that this entry is a snapshot entry.
    /// </summary>
    bool IsSnapshot => false;

    /// <summary>
    /// Gets UTC time of the log entry when it was created.
    /// </summary>
    DateTimeOffset Timestamp { get; }
}