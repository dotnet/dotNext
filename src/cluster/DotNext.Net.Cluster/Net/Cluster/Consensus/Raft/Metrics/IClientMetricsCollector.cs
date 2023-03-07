namespace DotNext.Net.Cluster.Consensus.Raft.Metrics;

/// <summary>
/// Optional interface that can be implemented by class derived from
/// <see cref="MetricsCollector"/> to collect metrics about response time reported
/// by the Raft client.
/// </summary>
[Obsolete("Use System.Diagnostics.Metrics infrastructure instead.", UrlFormat = "https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics")]
public interface IClientMetricsCollector
{
    /// <summary>
    /// Reports about response time.
    /// </summary>
    /// <param name="value">The response time.</param>
    void ReportResponseTime(TimeSpan value);
}