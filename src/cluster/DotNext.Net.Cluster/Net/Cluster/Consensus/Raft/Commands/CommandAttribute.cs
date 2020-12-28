using System;

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
        public Type? Formatter { get; set; }
    }
}