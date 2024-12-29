using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotNext.Buffers;
using DotNext.Text;

namespace DotNext.IO;

[ExcludeFromCodeCoverage]
internal static class DictionarySerializer
{
    internal static async Task SerializeAsync(IReadOnlyDictionary<string, string> dictionary, Stream output, Memory<byte> buffer)
    {
        // write count
        await output.WriteLittleEndianAsync(dictionary.Count, buffer);

        var context = new EncodingContext(Encoding.UTF8, reuseEncoder: true);

        // write pairs
        foreach (var (key, value) in dictionary)
        {
            await output.EncodeAsync(key.AsMemory(), context, LengthFormat.LittleEndian, buffer);
            await output.EncodeAsync(value.AsMemory(), context, LengthFormat.LittleEndian, buffer);
        }
    }

    internal static async Task<IReadOnlyDictionary<string, string>> DeserializeAsync(Stream input, Memory<byte> buffer)
    {
        var count = await input.ReadLittleEndianAsync<int>(buffer);
        var result = new Dictionary<string, string>(count);
        var context = new DecodingContext(Encoding.UTF8, reuseDecoder: true);

        while (--count >= 0)
        {
            using var key = await input.DecodeAsync(context, LengthFormat.LittleEndian, buffer);
            using var value = await input.DecodeAsync(context, LengthFormat.LittleEndian, buffer);
            result.Add(key.ToString(), value.ToString());
        }

        return result;
    }
}