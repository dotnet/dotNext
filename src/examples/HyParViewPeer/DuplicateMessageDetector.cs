using System.Collections.Specialized;
using System.Runtime.Caching;
using static System.Globalization.CultureInfo;

namespace HyParViewPeer;

internal sealed class DuplicateRequestDetector : MemoryCache
{
    private new const string Name = "DotNextRaftDuplicationDetector";

    private readonly TimeSpan expiration;
    private readonly object valuePlaceholder;

    public DuplicateRequestDetector()
        : base(Name, CreateConfiguration(TimeSpan.FromSeconds(10), 10), true)
    {
        expiration = TimeSpan.FromMinutes(1);
        valuePlaceholder = new();
    }

    private static NameValueCollection CreateConfiguration(TimeSpan pollingTime, long memoryLimitMB)
    {
        const string cacheMemoryLimitMegabytes = "CacheMemoryLimitMegabytes";
        const string pollingInterval = "PollingInterval";

        return new NameValueCollection
            {
                { cacheMemoryLimitMegabytes, memoryLimitMB.ToString(InvariantCulture) },
                { pollingInterval, pollingTime.ToString() },
            };
    }

    internal bool IsDuplicated(string messageId)
        => AddOrGetExisting(messageId, valuePlaceholder, DateTimeOffset.Now + expiration) is not null;
}