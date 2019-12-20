using System;
using System.Collections.Specialized;
using System.Runtime.Caching;
using static System.Globalization.CultureInfo;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    /*
        HTTP-based reliable messaging can be implemented as at-least-once delivery pattern.
        That's way we need to convert it into exactly-once delivery pattern through detection of duplicate messages
        This class allows to detect duplicate HTTP requests and drop them
     */
    internal sealed class DuplicateRequestDetector : MemoryCache
    {
        private new const string Name = "DotNextRaftDuplicationDetector";

        private readonly TimeSpan expiration;

        internal DuplicateRequestDetector(RequestJournalConfiguration config)
            : base(Name, CreateConfiguration(config.PollingInterval, config.MemoryLimit), true)
            => expiration = config.Expiration;

        private static NameValueCollection CreateConfiguration(TimeSpan pollingTime, long memoryLimitMB)
        {
            const string cacheMemoryLimitMegabytes = "CacheMemoryLimitMegabytes";
            const string pollingInterval = "PollingInterval";

            return new NameValueCollection
            {
                { cacheMemoryLimitMegabytes, memoryLimitMB.ToString(InvariantCulture) },
                { pollingInterval, pollingTime.ToString() }
            };
        }

        private readonly object valuePlaceholder = new object();

        /*
            Logic of this method:
            If cache returns the same value for this message then it was not added previously; otherwise, it is different message but with the same id
         */
        internal bool IsDuplicated(HttpMessage message)
            => AddOrGetExisting(message.Id, valuePlaceholder, DateTimeOffset.Now + expiration) != null;
    }
}