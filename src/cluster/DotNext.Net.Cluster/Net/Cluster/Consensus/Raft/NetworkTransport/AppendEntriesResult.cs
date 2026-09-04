using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Net.Cluster.Consensus.Raft.NetworkTransport;

using Buffers;
using Buffers.Binary;
using static Runtime.CompilerServices.AdvancedHelpers;

internal readonly struct AppendEntriesResult : IBinaryFormattable<AppendEntriesResult>
{
    internal const int Size = sizeof(long) + sizeof(long) + sizeof(byte);

    private readonly long term;
    private readonly ReplicationStatus status;

    internal AppendEntriesResult(in Result<ReplicationStatus> result)
    {
        term = result.Term;
        status = result.Value;
    }

    private AppendEntriesResult(long term, long lastIndex, HeartbeatResult result)
    {
        this.term = term;
        status = new() { LastIndex = lastIndex, Result = result };
    }
    
    static int IBinaryFormattable<AppendEntriesResult>.Size => Size;
    
    public void Format(Span<byte> destination)
    {
        var writer = new SpanWriter<byte>(destination);
        writer.WriteLittleEndian(term);
        writer.WriteLittleEndian(status.LastIndex);
        writer.Add() = (byte)status.Result;
    }

    public static AppendEntriesResult Parse(ReadOnlySpan<byte> source)
    {
        var reader = new SpanReader<byte>(source);
        return new(reader.ReadLittleEndian<long>(), reader.ReadLittleEndian<long>(), (HeartbeatResult)reader.Read());
    }

    public static implicit operator Result<ReplicationStatus>(in AppendEntriesResult result) => new()
    {
        Term = result.term,
        Value = result.status,
    };
}