using EndPoint = System.Net.EndPoint;

namespace DotNext.Net.Cluster.Consensus.Raft.Metrics;

/// <summary>
/// Optional interface that can be implemented by class derived from
/// <see cref="MetricsCollector"/> to collect metrics about response time reported
/// by the Raft client.
/// </summary>
public interface IClientMetricsCollector
{
    /// <summary>
    /// Reports about response time.
    /// </summary>
    /// <param name="value">The response time.</param>
    void ReportResponseTime(TimeSpan value); // TODO: Remove in the next version of .NEXT

    /// <summary>
    /// Reports about response time.
    /// </summary>
    /// <param name="value">The response time.</param>
    /// <param name="requestTag">The string that describes the request (request type, for instance).</param>
    /// <param name="address">The address of the remote endpoint.</param>
    void ReportResponseTime(TimeSpan value, string requestTag, EndPoint address) => ReportResponseTime(value);
}