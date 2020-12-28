using System;
using System.Reflection;

namespace DotNext.Net.Cluster.Consensus.Raft.Commands
{
    /// <summary>
    /// Marks target value type as the command of the database engine
    /// constructed on top of <see cref="PersistentState"/> type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class CommandAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new attribute.
        /// </summary>
        /// <param name="id">The unique identifier of the command.</param>
        public CommandAttribute(int id) => Id = id;

        /// <summary>
        /// Gets unique identifier of the command.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// Gets or sets the type implementing <see cref="ICommandFormatter{TCommand}"/> interface
        /// for the attributed type.
        /// </summary>
        /// <remarks>
        /// The formatter must have public parameterless constructor.
        /// </remarks>
        public Type? Formatter { get; set; }

        /// <summary>
        /// Gets the name of the static property or field declared in <see cref="Formatter"/> type
        /// which has the type implementing <see cref="ICommandFormatter{TCommand}"/> interface.
        /// </summary>
        /// <value></value>
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
            return property is null ? null : property.GetValue(null);
        }

        internal object? CreateFormatter()
            => Formatter is null ? null : CreateFormatter(Formatter, FormatterMember);
    }
}