namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents status of <see cref="ManualResetCompletionSource"/>.
/// </summary>
public enum ManualResetCompletionSourceStatus
{
    /// <summary>
    /// The source is ready to use.
    /// </summary>
    WaitForActivation = 0,

    /// <summary>
    /// The task is constructed.
    /// </summary>
    Activated,

    /// <summary>
    /// The source has been completed and waiting for consumption of the result.
    /// </summary>
    WaitForConsumption,

    /// <summary>
    /// The task has been consumed to the source can be reset to its initial state.
    /// </summary>
    Consumed,
}