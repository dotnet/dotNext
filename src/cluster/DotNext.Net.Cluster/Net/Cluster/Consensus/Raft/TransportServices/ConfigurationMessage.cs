namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;

internal static class ConfigurationMessage
{
    internal const int Size = sizeof(long) + sizeof(long);

    internal static void Write(ref SpanWriter<byte> writer, long fingerprint, long length)
    {
        writer.WriteLittleEndian(fingerprint);
        writer.WriteLittleEndian(length);
    }

    internal static (long Fingerprint, long Length) Read(ref SpanReader<byte> reader)
        => (reader.ReadLittleEndian<long>(isUnsigned: false), reader.ReadLittleEndian<long>(isUnsigned: false));
}