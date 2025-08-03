using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents multiplexing protocol options.
/// </summary>
public abstract class MultiplexingOptions
{
    private readonly int fragmentSize = 1380 - FragmentHeader.Size;
    private readonly PipeOptions options = PipeOptions.Default;

    /// <summary>
    /// Gets or sets the maximum size of the data encapsulated by the single packet.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is too small or large.</exception>
    public int FragmentSize
    {
        get => fragmentSize;
        init => fragmentSize = value is >= FragmentHeader.Size and <= ushort.MaxValue
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets buffering options.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public PipeOptions BufferOptions
    {
        get => options;
        init => options = value ?? throw new ArgumentNullException(nameof(value));
    }
}