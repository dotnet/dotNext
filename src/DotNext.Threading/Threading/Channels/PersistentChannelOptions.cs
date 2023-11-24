using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading.Channels;

namespace DotNext.Threading.Channels;

/// <summary>
/// Represents persistent channel options.
/// </summary>
public sealed class PersistentChannelOptions : ChannelOptions
{
    private const int DefaultCapacity = 1000;
    private const int DefaultBufferSize = 4096;
    private string location;
    private int bufferSize;
    private int capacity;

    /// <summary>
    /// Initializes a new options with default settings.
    /// </summary>
    public PersistentChannelOptions()
    {
        location = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        bufferSize = DefaultBufferSize;
        capacity = DefaultCapacity;
    }

    /// <summary>
    /// Gets or sets size of internal buffer used to perform I/O operations.
    /// </summary>
    public int BufferSize
    {
        get => bufferSize;
        set => bufferSize = value > 0 ? value : DefaultBufferSize;
    }

    /// <summary>
    /// Gets or sets path used to store queue files.
    /// </summary>
    public string Location
    {
        get => location;
        set => location = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets maximum number of messages per file.
    /// </summary>
    public int PartitionCapacity
    {
        get => capacity;
        set => capacity = value > 0 ? value : DefaultCapacity;
    }

    /// <summary>
    /// Gets or sets initial size of partition file, in bytes.
    /// </summary>
    /// <remarks>
    /// This property may help to avoid fragmentation of partition
    /// file on disk during writing.
    /// </remarks>
    public long InitialPartitionSize
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public TagList MeasurementTags
    {
        get;
        set;
    }

    /// <summary>
    /// Gets or sets a value indicating that <see cref="ChannelReader{T}.ReadAllAsync(CancellationToken)"/>
    /// must provide reliable (transactional) enumeration over persisted elements.
    /// </summary>
    /// <remarks>
    /// Reliable read means that the element obtained via <see cref="IAsyncEnumerator{T}.Current"/> is not marked
    /// as read before the next call of <see cref="IAsyncEnumerator{T}.MoveNextAsync"/>.
    /// Note that reliable enumeration affects the performance of asynchronous enumerator.
    /// </remarks>
    public bool ReliableEnumeration { get; set; }
}