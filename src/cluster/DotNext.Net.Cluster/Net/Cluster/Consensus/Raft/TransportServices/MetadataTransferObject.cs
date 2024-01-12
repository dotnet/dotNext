using System.Buffers;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices;

using Buffers;
using IO;
using Runtime.Serialization;
using Text;

[StructLayout(LayoutKind.Auto)]
internal readonly struct MetadataTransferObject : ISerializable<MetadataTransferObject>
{
    private const LengthFormat LengthEncoding = LengthFormat.Compressed;
    private readonly IReadOnlyDictionary<string, string>? metadata;

    internal MetadataTransferObject(IReadOnlyDictionary<string, string> metadata)
        => this.metadata = metadata;

    private static Encoding Encoding => Encoding.UTF8;

    internal IReadOnlyDictionary<string, string> Metadata => metadata ?? FrozenDictionary<string, string>.Empty;

    long? IDataTransferObject.Length => null;

    bool IDataTransferObject.IsReusable => true;

    private static void Write(IBufferWriter<byte> writer, IReadOnlyDictionary<string, string> metadata)
    {
        writer.WriteLittleEndian(metadata.Count);

        var context = new EncodingContext(Encoding, reuseEncoder: true);
        foreach (var (key, value) in metadata)
        {
            writer.Encode(key, context, lengthFormat: LengthEncoding);
            writer.Encode(value, context, lengthFormat: LengthEncoding);
        }
    }

    private static async ValueTask WriteAsync<TWriter>(TWriter writer, IReadOnlyDictionary<string, string> metadata, CancellationToken token)
        where TWriter : notnull, IAsyncBinaryWriter
    {
        await writer.WriteLittleEndianAsync(metadata.Count, token).ConfigureAwait(false);

        var context = new EncodingContext(Encoding, reuseEncoder: true);
        foreach (var (key, value) in metadata)
        {
            await writer.EncodeAsync(key.AsMemory(), context, lengthFormat: LengthEncoding, token).ConfigureAwait(false);
            await writer.EncodeAsync(value.AsMemory(), context, lengthFormat: LengthEncoding, token).ConfigureAwait(false);
        }
    }

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        ValueTask result;

        var buffer = writer.TryGetBufferWriter();
        if (buffer is null)
        {
            result = WriteAsync(writer, Metadata, token);
        }
        else
        {
            result = ValueTask.CompletedTask;
            try
            {
                Write(buffer, Metadata);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    private static bool TryGetSequenceReader<TReader>(TReader reader, out SequenceReader result)
        where TReader : notnull, IAsyncBinaryReader
    {
        if (typeof(TReader) == typeof(SequenceReader))
        {
            result = Unsafe.As<TReader, SequenceReader>(ref reader);
            return true;
        }

        if (reader.TryGetSequence(out var sequence))
        {
            result = new(sequence);
            return true;
        }

        result = default;
        return false;
    }

    private static MetadataTransferObject Read(ref SequenceReader reader)
    {
        var length = reader.ReadLittleEndian<int>();
        var output = new Dictionary<string, string>(length, StringComparer.Ordinal);
        var context = new DecodingContext(Encoding, reuseDecoder: true);
        while (--length >= 0)
        {
            // read key
            using var key = reader.Decode(context, LengthEncoding);

            // read value
            using var value = reader.Decode(context, LengthEncoding);

            // write pair to the dictionary
            output.Add(key.ToString(), value.ToString());
        }

        output.TrimExcess();
        return new(output);
    }

    private static async ValueTask<MetadataTransferObject> ReadAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
    {
        var length = await reader.ReadLittleEndianAsync<int>(token).ConfigureAwait(false);
        var output = new Dictionary<string, string>(length, StringComparer.Ordinal);
        var context = new DecodingContext(Encoding, reuseDecoder: true);
        while (--length >= 0)
        {
            // read key
            using var key = await reader.DecodeAsync(context, LengthEncoding, token: token).ConfigureAwait(false);

            // read value
            using var value = await reader.DecodeAsync(context, LengthEncoding, token: token).ConfigureAwait(false);

            // write pair to the dictionary
            output.Add(key.ToString(), value.ToString());
        }

        output.TrimExcess();
        return new(output);
    }

    public static ValueTask<MetadataTransferObject> ReadFromAsync<TReader>(TReader reader, CancellationToken token)
        where TReader : notnull, IAsyncBinaryReader
    {
        ValueTask<MetadataTransferObject> result;

        if (TryGetSequenceReader(reader, out var sequence))
        {
            try
            {
                result = new(Read(ref sequence));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<MetadataTransferObject>(e);
            }
        }
        else
        {
            result = ReadAsync(reader, token);
        }

        return result;
    }
}