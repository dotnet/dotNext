using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using Buffers.Binary;
using Membership;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct ConfigurationMessage(long Fingerprint, long Length) : IBinaryFormattable<ConfigurationMessage>
{
    internal const int Size = sizeof(long) + sizeof(long);

    static int IBinaryFormattable<ConfigurationMessage>.Size => Size;

    internal ConfigurationMessage(IClusterConfiguration config)
        : this(config.Fingerprint, config.Length)
    {
    }

    public void Format(Span<byte> output)
    {
        var writer = new SpanWriter<byte>(output);
        writer.WriteLittleEndian(Fingerprint);
        writer.WriteLittleEndian(Length);
    }

    public static ConfigurationMessage Parse(ReadOnlySpan<byte> input)
    {
        var reader = new SpanReader<byte>(input);
        return new(reader.ReadLittleEndian<long>(), reader.ReadLittleEndian<long>());
    }
}