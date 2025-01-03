using System.Text.Json.Serialization.Metadata;

namespace DotNext.Text.Json;

/// <summary>
/// Represents JSON serializable type.
/// </summary>
/// <typeparam name="TSelf">The type implementing this interface.</typeparam>
public interface IJsonSerializable<TSelf>
    where TSelf : IJsonSerializable<TSelf>
{
    /// <summary>
    /// Gets the type information required by serialization or deserialization process.
    /// </summary>
    static abstract JsonTypeInfo<TSelf> TypeInfo { get; }
}