namespace DotNext.Net.Cluster.Discovery.HyParView;

/// <summary>
/// Represents configuration of the HyParView peer.
/// </summary>
public interface IPeerConfiguration
{
    /// <summary>
    /// Gets the capacity of active view representing resolved peers.
    /// </summary>
    int ActiveViewCapacity { get; }

    /// <summary>
    /// Gets the capacity of backlog for peers.
    /// </summary>
    int PassiveViewCapacity { get; }

    /// <summary>
    /// Gets the maximum number of hops a ForwardJoin request is propagated.
    /// </summary>
    int ActiveRandomWalkLength { get; }

    /// <summary>
    /// Gets the value specifies at which point in the walk the peer is inserted into passive view.
    /// </summary>
    int PassiveRandomWalkLength { get; }

    /// <summary>
    /// Gets the number of peers from active view to be included into Shuffle message.
    /// </summary>
    int ShuffleActiveViewCount => Math.Max(ActiveViewCapacity / 2, 2);

    /// <summary>
    /// Gets the number of peers from passive view to be included into Shuffle message.
    /// </summary>
    int ShufflePassiveViewCount => Math.Max(PassiveViewCapacity / 2, 2);

    /// <summary>
    /// Gets the maximum number of hops a Shuffle message is propagated.
    /// </summary>
    int ShuffleRandomWalkLength => PassiveRandomWalkLength;

    /// <summary>
    /// Gets or sets shuffle period.
    /// </summary>
    /// <remarks>
    /// If <see langword="null"/> then <see cref="PeerController.EnqueueShuffleAsync(System.Threading.CancellationToken)"/> must be called
    /// manually when needed.
    /// </remarks>
    TimeSpan? ShufflePeriod { get; }

    /// <summary>
    /// Gets the capacity of internal queue used to process messages.
    /// </summary>
    int QueueCapacity => ActiveViewCapacity + PassiveViewCapacity;
}