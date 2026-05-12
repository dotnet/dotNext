namespace DotNext.Threading.Tasks;

/// <summary>
/// Represents an exception indicating that the delayed task is canceled safely without entering
/// the scheduled callback.
/// </summary>
public sealed class DelayedTaskCanceledException : OperationCanceledException
{
    internal DelayedTaskCanceledException(OperationCanceledException e)
        : base(e.Message, e, e.CancellationToken)
    {
    }
}