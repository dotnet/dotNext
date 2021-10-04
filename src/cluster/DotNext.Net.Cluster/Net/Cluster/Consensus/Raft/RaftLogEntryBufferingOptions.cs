namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers;
using IO;

/// <summary>
/// Represents options for creating buffered Raft log entries.
/// </summary>
public class RaftLogEntryBufferingOptions
{
    private const int DefaultMemoryThreshold = 32768;
    private const int DefaultFileBufferSize = 4096;
    private string? destinationPath;
    private int memoryThreshold = DefaultMemoryThreshold;
    private int fileBufferSize = DefaultFileBufferSize;

    /// <summary>
    /// Gets or sets full path to the directory used as temporary storage of
    /// large log entries.
    /// </summary>
    public string TempPath
    {
        get => string.IsNullOrEmpty(destinationPath) ? Path.GetTempPath() : destinationPath;
        set => destinationPath = value;
    }

    /// <summary>
    /// Gets or sets buffer size for internal I/O operations.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public int BufferSize
    {
        get => fileBufferSize;
        set => fileBufferSize = value > 0 ? fileBufferSize : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// The maximum size of log entry that can be stored in-memory without saving the content to the disk.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public int MemoryThreshold
    {
        get => memoryThreshold;
        set => memoryThreshold = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets memory allocator.
    /// </summary>
    public MemoryAllocator<byte>? MemoryAllocator
    {
        get;
        set;
    }

    internal string GetRandomFileName() => Path.Combine(TempPath, Path.GetRandomFileName());

    internal MemoryOwner<byte> RentBuffer() => MemoryAllocator.Invoke(BufferSize, false);

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