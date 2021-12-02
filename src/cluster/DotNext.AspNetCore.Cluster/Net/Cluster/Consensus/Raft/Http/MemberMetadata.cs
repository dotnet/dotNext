using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

internal sealed class MemberMetadata : Dictionary<string, string>
{
    internal static readonly JsonTypeInfo<MemberMetadata> TypeInfo;

    static MemberMetadata()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false, WriteIndented = false };

        var info = new JsonCollectionInfoValues<MemberMetadata>
        {
            ObjectCreator = Activator.CreateInstance<MemberMetadata>,
            KeyInfo = JsonMetadataServices.CreateValueInfo<string>(options, JsonMetadataServices.StringConverter),
            ElementInfo = JsonMetadataServices.CreateValueInfo<KeyValuePair<string, string>>(options, JsonMetadataServices.ObjectConverter),
            NumberHandling = default,
            SerializeHandler = null,
        };

        TypeInfo = JsonMetadataServices.CreateIDictionaryInfo<MemberMetadata, string, string>(options, info);
    }

    internal MemberMetadata(IDictionary<string, string> properties)
        : base(properties, StringComparer.Ordinal)
    {
    }

    public MemberMetadata()
        : base(StringComparer.Ordinal)
    {
    }
}