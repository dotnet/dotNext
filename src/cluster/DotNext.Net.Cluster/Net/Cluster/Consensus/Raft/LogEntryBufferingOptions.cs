namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;

/// <summary>
/// Represents options for creating buffered Raft log entries.
/// </summary>
public class LogEntryBufferingOptions
{
    private const int DefaultMemoryThreshold = 32768;
    private const int DefaultFileBufferSize = 4096;

    /// <summary>
    /// Gets or sets full path to the directory used as temporary storage of
    /// large log entries.
    /// </summary>
    public string TempPath
    {
        get => field is { Length: > 0 } ? field : Path.GetTempPath();
        set;
    }

    /// <summary>
    /// Gets or sets buffer size for internal I/O operations.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public int BufferSize
    {
        get;
        set => field = value > 0 ? field : throw new ArgumentOutOfRangeException(nameof(value));
    } = DefaultFileBufferSize;

    /// <summary>
    /// The maximum size of log entry that can be stored in-memory without saving the content to the disk.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public int MemoryThreshold
    {
        get;
        set => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = DefaultMemoryThreshold;

    /// <summary>
    /// Gets or sets memory allocator.
    /// </summary>
    public MemoryAllocator<byte> MemoryAllocator
    {
        get => field.DefaultIfNull;
        set;
    }

    internal string GetRandomFileName() => Path.Combine(TempPath, Path.GetRandomFileName());

    internal MemoryOwner<byte> RentBuffer() => MemoryAllocator.AllocateAtLeast(BufferSize);

    internal FileBufferingWriter CreateBufferingWriter()
    {
        var options = new FileBufferingWriter.Options
        {
            MemoryAllocator = MemoryAllocator,
            MemoryThreshold = MemoryThreshold,
            AsyncIO = true,
            FileName = GetRandomFileName(),
            FileBufferSize = BufferSize,
        };

        return new FileBufferingWriter(options);
    }
}