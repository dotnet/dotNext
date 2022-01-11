namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class ConfigurationMessage
{
    internal const int Size = sizeof(long) + sizeof(long);

    internal static void Write(ref SpanWriter<byte> writer, long fingerprint, long length)
    {
        writer.WriteInt64(fingerprint, true);
        writer.WriteInt64(length, true);
    }

    internal static (long Fingerprint, long Length) Read(ref SpanReader<byte> reader)
        => (reader.ReadInt64(true), reader.ReadInt64(true));
}