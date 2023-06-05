namespace DotNext.Diagnostics;

/// <summary>
/// Represents failure detector that can be used to determine availability of the particular resource in
/// distributed environment such as a cluster to peer-to-peer network.
/// </summary>
public interface IFailureDetector : IResettable
{
    /// <summary>
    /// Indicates that the resource associated with this detector is considered to be up
    /// and healthy.
    /// </summary>
    bool IsHealthy { get; }

    /// <summary>
    /// Indicates that this detector has received any heartbeats and started monitoring of the resource.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Notifies that this detector received a heartbeat from the associated resource.
    /// </summary>
    void ReportHeartbeat();
}