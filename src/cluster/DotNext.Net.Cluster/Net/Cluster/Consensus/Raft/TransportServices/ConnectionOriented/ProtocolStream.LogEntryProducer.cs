using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using IO;
using IO.Log;

internal partial class ProtocolStream : IRaftLogEntry
{
    private int entriesCount;
    private LogEntryMetadata metadata;
    private bool consumed;

    long? IDataTransferObject.Length => null;

    bool IDataTransferObject.IsReusable => false;

    DateTimeOffset ILogEntry.Timestamp => metadata.Timestamp;

    int? IRaftLogEntry.CommandId => metadata.CommandId;

    long IRaftLogEntry.Term => metadata.Term;
}