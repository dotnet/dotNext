using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Reflection;

/// <summary>
/// Represents reflected field.
/// </summary>
public interface IField : IMember<FieldInfo>
{
    /// <summary>
    /// Indicates that field is read-only.
    /// </summary>
    bool IsReadOnly => Metadata is { IsInitOnly: true } or { IsLiteral: true };
}

/// <summary>
/// Represents static field.
/// </summary>
/// <typeparam name="TValue">Type of field .</typeparam>
public interface IField<TValue> : IField
{
    /// <summary>
    /// Obtains managed pointer to the static field.
    /// </summary>
    /// <value>The managed pointer to the static field.</value>
    ref TValue? Value { get; }
}

/// <summary>
/// Represents instance field.
/// </summary>
/// <typeparam name="T">Field declaring type.</typeparam>
/// <typeparam name="TValue">Type of field.</typeparam>
public interface IField<T, TValue> : IField
{
    /// <summary>
    /// Obtains managed pointer to the field.
    /// </summary>
    /// <param name="this">A reference to <c>this</c> parameter.</param>
    /// <returns>The managed pointer to the instance field.</returns>
    ref TValue? this[[DisallowNull] in T @this] { get; }
}