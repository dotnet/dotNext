namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class MemberMetadata : Dictionary<string, string>
{
    internal MemberMetadata(IDictionary<string, string> properties)
        : base(properties, StringComparer.Ordinal)
    {
    }

    public MemberMetadata()
        : base(StringComparer.Ordinal)
    {
    }
}