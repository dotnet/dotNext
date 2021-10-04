namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Runtime.Serialization;

/// <summary>
/// Registers command type in the interpreter.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class CommandAttribute : Attribute
{
    /// <summary>
    /// Initializes a new attribute.
    /// </summary>
    /// <param name="id">The identifier of the log entry.</param>
    protected CommandAttribute(int id) => Id = id;

    /// <summary>
    /// Gets unique identifier of the log entry.
    /// </summary>
    public int Id { get; }
}

/// <summary>
/// Registers command type in the interpreter.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
public sealed class CommandAttribute<TCommand> : CommandAttribute
    where TCommand : notnull, ISerializable<TCommand>
{
    /// <summary>
    /// Initializes a new attribute.
    /// </summary>
    /// <param name="id">The identifier of the log entry.</param>
    public CommandAttribute(int id)
        : base(id)
    {
    }
}