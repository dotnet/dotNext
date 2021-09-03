using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace DotNext.Runtime.Serialization
{
    /// <summary>
    /// Represents base class for attributes marking the serializable types using
    /// <see cref="IFormatter{T}"/> formatters.
    /// </summary>
    public abstract class SerializableAttribute : Attribute, ISupplier<object?>
    {
        /// <summary>
        /// Initializes a new attribute.
        /// </summary>
        protected SerializableAttribute()
        {
        }

        /// <summary>
        /// Gets or sets the type implementing <see cref="IFormatter{T}"/> interface
        /// for the attributed type.
        /// </summary>
        /// <remarks>
        /// The formatter must have public parameterless constructor.
        /// </remarks>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)]
        public Type? Formatter { get; set; }

        /// <summary>
        /// Gets the name of the public static property or field declared in <see cref="Formatter"/> type
        /// which has the type implementing <see cref="IFormatter{T}"/> interface.
        /// </summary>
        public string? FormatterMember { get; set; }

        private static object? CreateFormatter(Type formatterType, string? memberName)
        {
            if (string.IsNullOrEmpty(memberName))
                return Activator.CreateInstance(formatterType);

            const BindingFlags publicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

            var field = formatterType.GetField(memberName, publicStatic);
            if (field is not null)
                return field.GetValue(null);

            var property = formatterType.GetProperty(memberName, publicStatic);
            return property?.GetValue(null);
        }

        internal object? CreateFormatter()
            => Formatter is null ? null : CreateFormatter(Formatter, FormatterMember);

        /// <inheritdoc/>
        object? ISupplier<object?>.Invoke() => CreateFormatter();
    }
}