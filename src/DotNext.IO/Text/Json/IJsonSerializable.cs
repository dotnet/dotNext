using System.Runtime.Versioning;
using System.Text.Json.Serialization.Metadata;

namespace DotNext.Text.Json;

/// <summary>
/// Represents JSON serializable type.
/// </summary>
/// <typeparam name="TSelf">The type implementing this interface.</typeparam>
[RequiresPreviewFeatures]
public interface IJsonSerializable<TSelf>
    where TSelf : notnull, IJsonSerializable<TSelf>
{
    /// <summary>
    /// Gets the type information required by serialization or deserialization process.
    /// </summary>
    static abstract JsonTypeInfo<TSelf> TypeInfo { get; }
}