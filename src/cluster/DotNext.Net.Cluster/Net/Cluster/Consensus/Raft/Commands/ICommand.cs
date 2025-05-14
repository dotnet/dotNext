namespace DotNext.Net.Cluster.Consensus.Raft.Commands;

using Runtime.Serialization;

/// <summary>
/// Represents the state machine command.
/// </summary>
/// <typeparam name="TSelf">The implementing type.</typeparam>
public interface ICommand<TSelf> : ISerializable<TSelf>
    where TSelf : ICommand<TSelf>
{
    /// <summary>
    /// Gets the identifier of the command.
    /// </summary>
    static abstract int Id { get; }

    /// <summary>
    /// Gets a value indicating that this command is a snapshot handler.
    /// </summary>
    static virtual bool IsSnapshot => false;
}