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

        private readonly TimeSpan evictionTime;

        internal DuplicateRequestDetector(TimeSpan evictionTime, TimeSpan pollingTime, long memoryLimitMB)
            : base(Name, CreateConfiguration(pollingTime, memoryLimitMB), true)
            => this.evictionTime = evictionTime;

        private static NameValueCollection CreateConfiguration(TimeSpan pollingTime, long memoryLimitMB)
        {
            const string CacheMemoryLimitMegabytes = nameof(CacheMemoryLimitMegabytes);
            const string PollingInterval = nameof(PollingInterval);

            return new NameValueCollection
            {
                { CacheMemoryLimitMegabytes, Convert.ToString(memoryLimitMB, InvariantCulture) },
                { PollingInterval, pollingTime.ToString() }
            };
        }

        /*
            Logic of this method:
            If cache returns the same value for this message then it was not added previously; otherwise, it is different message but with the same id
         */
        internal bool IsDuplicate(HttpMessage message)
            => !ReferenceEquals(message.UniqueReference, AddOrGetExisting(message.Id, message.UniqueReference, DateTimeOffset.Now + evictionTime));
    }
}