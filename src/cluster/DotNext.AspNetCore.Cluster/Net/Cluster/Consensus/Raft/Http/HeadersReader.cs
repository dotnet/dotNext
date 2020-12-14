using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal delegate bool HeadersReader<THeaders>(string headerName, [NotNullWhen(true)]out THeaders? headers)
        where THeaders : IEnumerable<string>;

    internal delegate bool ValueParser<T>(string str, [MaybeNullWhen(false)] out T value);
}
