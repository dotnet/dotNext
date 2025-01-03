namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Runtime.Serialization;

/// <summary>
/// Registers command type in the interpreter.
/// </summary>
/// <param name="id">The identifier of the log entry.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public abstract class CommandAttribute(int id) : Attribute
{
    /// <summary>
    /// Gets unique identifier of the log entry.
    /// </summary>
    public int Id => id;

    internal abstract Type CommandType { get; }
}

/// <summary>
/// Registers command type in the interpreter.
/// </summary>
/// <typeparam name="TCommand">The type of the command.</typeparam>
/// <remarks>
/// Initializes a new attribute.
/// </remarks>
/// <param name="id">The identifier of the log entry.</param>
public sealed class CommandAttribute<TCommand>(int id) : CommandAttribute(id)
    where TCommand : ISerializable<TCommand>
{
    internal override Type CommandType => typeof(TCommand);
}