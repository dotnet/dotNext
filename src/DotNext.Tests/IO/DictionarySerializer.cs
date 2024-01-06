using System.Diagnostics.CodeAnalysis;
using System.Text;
using DotNext.Buffers;

namespace DotNext.IO;

[ExcludeFromCodeCoverage]
internal static class DictionarySerializer
{
    internal static async Task SerializeAsync(IReadOnlyDictionary<string, string> dictionary, Stream output, Memory<byte> buffer)
    {
        // write count
        await output.WriteLittleEndianAsync(dictionary.Count, buffer);

        // write pairs
        foreach (var (key, value) in dictionary)
        {
            await output.EncodeAsync(key.AsMemory(), Encoding.UTF8, LengthFormat.LittleEndian, buffer);
            await output.EncodeAsync(value.AsMemory(), Encoding.UTF8, LengthFormat.LittleEndian, buffer);
        }
    }

    internal static async Task<IReadOnlyDictionary<string, string>> DeserializeAsync(Stream input, Memory<byte> buffer)
    {
        var count = await input.ReadLittleEndianAsync<int>(buffer);
        var result = new Dictionary<string, string>(count);

        while (--count >= 0)
        {
            using var key = await input.DecodeAsync(Encoding.UTF8, LengthFormat.LittleEndian, buffer);
            using var value = await input.DecodeAsync(Encoding.UTF8, LengthFormat.LittleEndian, buffer);
            result.Add(key.ToString(), value.ToString());
        }

        return result;
    }
}