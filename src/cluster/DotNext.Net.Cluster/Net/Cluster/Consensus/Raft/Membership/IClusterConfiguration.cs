namespace DotNext.Net.Cluster.Consensus.Raft.Membership
{
    using IO;

    /// <summary>
    /// Represents a snapshot of cluster configuration.
    /// </summary>
    public interface IClusterConfiguration : IDataTransferObject
    {
        /// <summary>
        /// Gets fingerprint of this configuration that uniquely identifies its content.
        /// </summary>
        long Fingerprint { get; }

        /// <summary>
        /// Gets length of the configuration, in bytes.
        /// </summary>
        new long Length { get; }

        /// <inheritdoc />
        long? IDataTransferObject.Length => Length;
    }
}