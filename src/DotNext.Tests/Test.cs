using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    [LogBeforeAfterTest]
    public abstract class Test : Assert
    {
        private protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

        private protected static T SerializeDeserialize<T>(T value)
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