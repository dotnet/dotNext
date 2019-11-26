using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    internal static class SerializationTestHelper
    {
        internal static T SerializeDeserialize<T>(T value)
            where T : ISerializable
        {
            IFormatter formatter = new BinaryFormatter();
            using var ms = new MemoryStream(2048);
            formatter.Serialize(ms, value);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
        }
    }
}
