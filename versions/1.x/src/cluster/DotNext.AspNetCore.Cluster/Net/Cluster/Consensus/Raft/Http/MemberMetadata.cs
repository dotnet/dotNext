using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    [CollectionDataContract(KeyName = "Name", ValueName = "Value")]
    internal sealed class MemberMetadata : Dictionary<string, string>
    {
        internal MemberMetadata(IDictionary<string, string> properties)
            : base(properties, StringComparer.Ordinal)
        {
        }

        internal MemberMetadata()
            : base(StringComparer.Ordinal)
        {
        }
    }
}
