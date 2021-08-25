using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace DotNext.IO
{
    [ExcludeFromCodeCoverage]
    internal static class DictionarySerializer
    {
        internal static void Serialize(IReadOnlyDictionary<string, string> dictionary, Stream output)
        {
            // write count
            output.Write(dictionary.Count);

            // write pairs
            foreach (var (key, value) in dictionary)
            {
                output.WriteString(key, Encoding.UTF8, LengthFormat.Plain);
                output.WriteString(value, Encoding.UTF8, LengthFormat.Plain);
            }
        }

        internal static IReadOnlyDictionary<string, string> Deserialize(Stream input)
        {
            var count = input.Read<int>();
            var result = new Dictionary<string, string>(count);

            while (--count >= 0)
            {
                var key = input.ReadString(LengthFormat.Plain, Encoding.UTF8);
                var value = input.ReadString(LengthFormat.Plain, Encoding.UTF8);
                result.Add(key, value);
            }

            return result;
        }
    }
}