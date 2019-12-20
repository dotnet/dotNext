using System.Collections.Generic;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal delegate bool HeadersReader<THeaders>(string headerName, out THeaders headers)
        where THeaders : IEnumerable<string>;

    internal delegate bool ValueParser<T>(string str, out T value);
}
