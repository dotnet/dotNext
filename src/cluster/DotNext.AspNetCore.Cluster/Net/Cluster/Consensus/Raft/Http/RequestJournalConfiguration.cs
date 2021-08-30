using System;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    /// <summary>
    /// Represents configuration of request journal used to avoid duplicate one-way messages.
    /// </summary>
    public sealed class RequestJournalConfiguration
    {
        /// <summary>
        /// Gets or sets memory limit (in MB) for the journal of input requests
        /// used to detect duplicate requests.
        /// </summary>
        public long MemoryLimit { get; set; } = 10L;

        /// <summary>
        /// Gets or sets the time interval after which the request journal compares the current memory load against the absolute memory limits.
        /// </summary>
        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Gets or sets expiration time of the single request.
        /// </summary>
        public TimeSpan Expiration { get; set; } = TimeSpan.FromSeconds(10);
    }
}
