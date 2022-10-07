using static System.Threading.Timeout;

namespace DotNext.Workflow;

/// <summary>
/// Represents activity options.
/// </summary>
public class ActivityOptions
{
    internal static readonly ActivityOptions Default = new();

    private readonly TimeSpan timeout = InfiniteTimeSpan;

    /// <summary>
    /// Gets or sets activity timeout.
    /// </summary>
    public TimeSpan Timeout
    {
        get => timeout;
        init
        {
            if (value < TimeSpan.Zero && value != InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(value));

            timeout = value;
        }
    }
}