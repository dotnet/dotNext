using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents reflected field.
    /// </summary>
    public interface IField : IMember<FieldInfo>
    {
        /// <summary>
        /// Indicates that field is read-only.
        /// </summary>
        bool IsReadOnly { get; }
    }

    /// <summary>
    /// Represents static field.
    /// </summary>
    /// <typeparam name="F">Type of field .</typeparam>
    public interface IField<F> : IField
    {
        /// <summary>
        /// Obtains managed pointer to the static field.
        /// </summary>
        /// <value>The managed pointer to the static field.</value>
        ref F Value { get; }
    }

    /// <summary>
    /// Represents instance field.
    /// </summary>
    /// <typeparam name="T">Field declaring type.</typeparam>
    /// <typeparam name="F">Type of field.</typeparam>
    public interface IField<T, F> : IField
    {
        /// <summary>
        /// Obtains managed pointer to the field.
        /// </summary>
        /// <param name="this">A reference to <c>this</c> parameter.</param>
        /// <returns>The managed pointer to the instance field.</returns>
        ref F this[in T @this] { get; }
    }
}
