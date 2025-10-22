using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;

/// <summary>
/// Represents builder of contiguous block of memory that may
/// switch to file as its backing store.
/// </summary>
/// <remarks>
/// This class can be used to write buffered content to a target <see cref="Stream"/>
/// or <see cref="IBufferWriter{T}"/>. Additionally, it's possible to get buffered
/// content as <see cref="Memory{T}"/>. If memory threshold was not reached then
/// returned <see cref="Memory{T}"/> instance references bytes in memory. Otherwise,
/// it references memory-mapped file.
/// </remarks>
public sealed partial class FileBufferingWriter : ModernStream, IBufferWriter<byte>, IGrowableBuffer<byte>
{
    [StructLayout(LayoutKind.Auto)]
    private readonly struct ReadSession : IDisposable
    {
        private readonly WeakReference refHolder;

        internal ReadSession(WeakReference obj)
            => refHolder = obj;

        public void Dispose()
        {
            if (refHolder is not null)
                refHolder.Target = null;
        }
    }

    private sealed unsafe class NativeMemoryManager : MemoryManager<byte>
    {
        private int length;
        private ReadSession session;
        private void* ptr;

        internal NativeMemoryManager(FileBufferingWriter writer, int length)
        {
            Debug.Assert(length > 0);
            Debug.Assert(writer.fileBackend is not null);

            this.length = length;
            ptr = NativeMemory.Alloc((nuint)length);
            session = writer.EnterReadMode(this);

            Debug.Assert(writer.IsReading);
        }

        internal void SetLength(int value)
        {
            Debug.Assert(value > 0);

            length = value;
        }

        public override Span<byte> GetSpan()
        {
            ObjectDisposedException.ThrowIf(ptr is null, this);
            return new(ptr, length);
        }

        public override Memory<byte> Memory => CreateMemory(length);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ObjectDisposedException.ThrowIf(ptr is null, this);
            return new(Unsafe.Add<byte>(ptr, elementIndex));
        }

        public override void Unpin() => ObjectDisposedException.ThrowIf(ptr is null, this);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                session.Dispose();
                session = default;
            }

            if (ptr is not null)
                NativeMemory.Free(ptr);

            ptr = null;
            length = 0;
        }
    }

    private sealed class BufferedMemoryManager : MemoryManager<byte>
    {
        private ReadSession session;
        private Memory<byte> memory;

        internal BufferedMemoryManager()
        {
            // no need to initialize memory block as empty block
        }

        internal BufferedMemoryManager(FileBufferingWriter writer, in Range range)
        {
            memory = writer.WrittenMemory[range];
            session = writer.EnterReadMode(this);
            Debug.Assert(writer.IsReading);
        }

        public override Span<byte> GetSpan()
            => memory.Span;

        public override Memory<byte> Memory => memory;

        public override MemoryHandle Pin(int elementIndex = 0)
            => memory.Slice(elementIndex).Pin();

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                memory = default;
                session.Dispose();
                session = default;
            }
        }
    }

    private enum MemoryEvaluationResult
    {
        Success = 0,
        PersistExistingBuffer,
        PersistAll,
    }

    private static readonly Histogram<int> AllocationMeter;

    static FileBufferingWriter()
    {
        var meter = new Meter("DotNext.Buffers.FileBufferingWriter");
        AllocationMeter = meter.CreateHistogram<int>("buffer-size", unit: "B", "In-Memory Buffer Size");
    }

    private readonly TagList measurementTags;
    private readonly BackingFileProvider fileProvider;
    private readonly int memoryThreshold;
    private readonly MemoryAllocator<byte>? allocator;
    private MemoryOwner<byte> buffer;
    private int position;
    private string? fileName;
    private SafeFileHandle? fileBackend;
    private long filePosition;

    // If null or .Target is null then there is no active readers.
    // Weak reference allows to track leaked readers when Dispose() was not called on them
    private WeakReference? reader;

    /// <summary>
    /// Initializes a new writer.
    /// </summary>
    /// <param name="allocator">The allocator of internal buffer.</param>
    /// <param name="memoryThreshold">The maximum amount of memory in bytes to allocate before switching to a file on disk.</param>
    /// <param name="initialCapacity">Initial capacity of internal buffer. Should not be greater than <paramref name="memoryThreshold"/>.</param>
    /// <param name="tempDir">
    /// The location of the directory to write buffered contents to.
    /// When unspecified, uses the value specified by the environment variable <c>ASPNETCORE_TEMP</c> if available, otherwise
    /// uses the value returned by <see cref="Path.GetTempPath"/>.
    /// </param>
    /// <param name="asyncIO"><see langword="true"/> if you will use asynchronous methods of the instance; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="memoryThreshold"/> is less than or equal to zero; or <paramref name="initialCapacity"/> is less than zero or greater than <paramref name="memoryThreshold"/>.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="tempDir"/> doesn't exist.</exception>
    public FileBufferingWriter(MemoryAllocator<byte>? allocator = null, int memoryThreshold = Options.DefaultMemoryThreshold, int initialCapacity = 0, string? tempDir = null, bool asyncIO = true)
        : this(new Options { MemoryAllocator = allocator, MemoryThreshold = memoryThreshold, InitialCapacity = initialCapacity, TempDir = tempDir, AsyncIO = asyncIO })
    {
    }

    /// <summary>
    /// Initializes a new writer.
    /// </summary>
    /// <param name="options">Buffer writer options.</param>
    /// <exception cref="DirectoryNotFoundException">Temporary directory for the backing file doesn't exist.</exception>
    public FileBufferingWriter(in Options options)
    {
        if (options.UseTemporaryFile && !Directory.Exists(options.Path))
            throw new DirectoryNotFoundException(ExceptionMessages.DirectoryNotFound(options.Path));

        allocator = options.MemoryAllocator;
        var memoryThreshold = options.MemoryThreshold;
        if (options.InitialCapacity > 0)
        {
            buffer = allocator.AllocateAtLeast(options.InitialCapacity);
            if (buffer.Length > memoryThreshold)
                memoryThreshold = buffer.Length < Array.MaxLength ? buffer.Length + 1 : Array.MaxLength;
        }

        this.memoryThreshold = memoryThreshold;
        fileProvider = new BackingFileProvider(in options);
        measurementTags = options.MeasurementTags;

        writeCallback = OnWrite;
        writeAndFlushCallback = OnWriteAndFlush;
        writeAndCopyCallback = OnWriteAndCopy;
    }

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override long Length => position + filePosition;

    /// <inheritdoc />
    long IGrowableBuffer<byte>.WrittenCount => Length;

    /// <inheritdoc />
    void IGrowableBuffer<byte>.CopyTo<TConsumer>(TConsumer consumer)
        => CopyTo(consumer, Options.DefaultFileBufferSize, CancellationToken.None);

    private bool IsReading => reader?.Target is not null;

    [MemberNotNull(nameof(reader))]
    private ReadSession EnterReadMode(IDisposable obj)
    {
        if (reader is { } refHolder)
        {
            refHolder.Target = obj;
        }
        else
        {
            refHolder = reader = new(obj, trackResurrection: false);
        }

        return new(refHolder);
    }

    /// <summary>
    /// Removes all written data.
    /// </summary>
    /// <param name="reuseBuffer"><see langword="true"/> to keep internal buffer alive; otherwise, <see langword="false"/>.</param>
    /// <exception cref="InvalidOperationException">Attempt to cleanup this writer while reading.</exception>
    public void Clear(bool reuseBuffer = true)
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        ClearCore(reuseBuffer);
    }

    /// <inheritdoc/>
    void IGrowableBuffer<byte>.Clear() => Clear(reuseBuffer: false);

    private void ClearCore(bool reuseBuffer)
    {
        if (!reuseBuffer)
        {
            buffer.Dispose();
        }

        if (fileBackend is not null)
        {
            fileBackend.Dispose();
            fileBackend = null;
        }

        fileName = null;
        filePosition = position = 0;
    }

    /// <inheritdoc/>
    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        switch (sizeHint)
        {
            case < 0:
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            case 0:
                sizeHint = Math.Max(1, buffer.Length - position);
                break;
        }

        switch (PrepareMemory(sizeHint, out var result))
        {
            case MemoryEvaluationResult.Success:
                break;
            case MemoryEvaluationResult.PersistExistingBuffer:
                PersistBuffer(flushToDisk: false);
                result = buffer.Memory.Slice(0, sizeHint);
                break;
            default:
                throw new InsufficientMemoryException();
        }

        return result;
    }

    /// <inheritdoc/>
    public Span<byte> GetSpan(int sizeHint = 0)
        => GetMemory(sizeHint).Span;

    /// <inheritdoc/>
    public void Advance(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (position > buffer.Length - count)
            ThrowInvalidOperationException();

        position += count;

        [DoesNotReturn]
        [StackTraceHidden]
        static void ThrowInvalidOperationException()
            => throw new InvalidOperationException();
    }

    private MemoryEvaluationResult PrepareMemory(int size, out Memory<byte> output)
    {
        var newSize = position + size;

        if ((uint)newSize > (uint)memoryThreshold)
        {
            output = default;
            return size <= memoryThreshold ? MemoryEvaluationResult.PersistExistingBuffer : MemoryEvaluationResult.PersistAll;
        }

        var bufLen = buffer.Length;

        // expand buffer if necessary
        if (bufLen < newSize)
        {
            bufLen <<= 1; // optimistically doubles buffer size to reduce the number of memory rentals
            if ((uint)bufLen > (uint)newSize && (uint)bufLen <= (uint)memoryThreshold)
                newSize = bufLen;

            buffer.Resize(newSize, allocator);
            AllocationMeter.Record(buffer.Length, measurementTags);
        }

        output = buffer.Memory.Slice(position, size);
        return MemoryEvaluationResult.Success;
    }

    private bool HasBufferedData => position > 0;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Memory<byte> WrittenMemory => buffer.Memory.Slice(0, position);

    private ReadOnlySpan<byte> WrittenSpan => buffer.Span.Slice(0, position);

    [MemberNotNull(nameof(fileBackend))]
    private ValueTask PersistBufferAsync(bool flushToDisk, CancellationToken token)
    {
        Debug.Assert(HasBufferedData);

        EnsureBackingStore();
        return Submit(RandomAccess.WriteAsync(fileBackend, WrittenMemory, filePosition, token), flushToDisk ? writeAndCopyCallback : writeCallback);
    }

    [MemberNotNull(nameof(fileBackend))]
    private void PersistBuffer(bool flushToDisk)
    {
        Debug.Assert(HasBufferedData);

        EnsureBackingStore();
        RandomAccess.Write(fileBackend, WrittenSpan, filePosition);

        filePosition += position;
        position = 0;
        if (flushToDisk)
            RandomAccess.FlushToDisk(fileBackend);
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token = default)
    {
        if (IsReading)
            return ValueTask.FromException(new InvalidOperationException(ExceptionMessages.WriterInReadMode));

        switch (PrepareMemory(input.Length, out var output))
        {
            default:
                return ValueTask.CompletedTask;
            case MemoryEvaluationResult.Success:
                input.CopyTo(output);
                position += input.Length;
                goto default;
            case MemoryEvaluationResult.PersistExistingBuffer:
                return PersistExistingBufferAsync(input, token);
            case MemoryEvaluationResult.PersistAll:
                return PersistAllAsync(input, token);
        }
    }

    private ValueTask PersistExistingBufferAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        Debug.Assert(HasBufferedData);

        EnsureBackingStore();
        secondBuffer = input;
        return Submit(RandomAccess.WriteAsync(fileBackend, WrittenMemory, filePosition, token), writeAndCopyCallback);
    }

    private ValueTask PersistAllAsync(ReadOnlyMemory<byte> input, CancellationToken token)
    {
        EnsureBackingStore();

        ValueTask task;
        if (HasBufferedData)
        {
            secondBuffer = input;
            task = RandomAccess.WriteAsync(fileBackend, (IReadOnlyList<ReadOnlyMemory<byte>>)this.As<IDynamicInterfaceCastable>(), filePosition, token);
        }
        else
        {
            position = input.Length;
            task = RandomAccess.WriteAsync(fileBackend, input, filePosition, token);
        }

        return Submit(task, writeCallback);
    }

    /// <inheritdoc cref="Stream.Write(ReadOnlySpan{byte})"/>
    public override void Write(ReadOnlySpan<byte> input)
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        switch (PrepareMemory(input.Length, out var output))
        {
            case MemoryEvaluationResult.Success:
                input.CopyTo(output.Span);
                position += input.Length;
                break;
            case MemoryEvaluationResult.PersistExistingBuffer:
                PersistBuffer(flushToDisk: false);
                input.CopyTo(this.buffer.Span);
                position = input.Length;
                break;
            case MemoryEvaluationResult.PersistAll:
                if (HasBufferedData)
                    PersistBuffer(flushToDisk: false);
                else
                    EnsureBackingStore();

                RandomAccess.Write(fileBackend, input, filePosition);
                filePosition += input.Length;
                break;
        }
    }

    [MemberNotNull(nameof(fileBackend))]
    private void EnsureBackingStore() => fileBackend ??= fileProvider.CreateBackingFileHandle(position, out fileName);

    /// <inheritdoc cref="Stream.Flush()"/>
    public override void Flush() => Flush(flushToDisk: false);

    /// <summary>
    /// Flushes the internal buffer with the file and optionally
    /// synchronize a file's in-core state with storage device.
    /// </summary>
    /// <param name="flushToDisk"><see langword="true"/> to synchronize a file's in-core state with storage device; otherwise, <see langword="false"/>.</param>
    public void Flush(bool flushToDisk)
    {
        if (fileBackend is null)
        {
            // jump to exit
        }
        else if (HasBufferedData)
        {
            PersistBuffer(flushToDisk);
        }
        else if (flushToDisk)
        {
            RandomAccess.FlushToDisk(fileBackend);
        }
    }

    /// <summary>
    /// Flushes the internal buffer with the file and optionally
    /// synchronize a file's in-core state with storage device.
    /// </summary>
    /// <param name="flushToDisk"><see langword="true"/> to synchronize a file's in-core state with storage device; otherwise, <see langword="false"/>.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <returns>The asynchronous result of the operation.</returns>
    public ValueTask FlushAsync(bool flushToDisk, CancellationToken token = default)
    {
        var result = ValueTask.CompletedTask;

        if (fileBackend is null)
        {
            // jump to exit
        }
        else if (HasBufferedData)
        {
            result = PersistBufferAsync(flushToDisk, token);
        }
        else if (flushToDisk)
        {
            try
            {
                RandomAccess.FlushToDisk(fileBackend);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }

        return result;
    }

    /// <inheritdoc cref="Stream.FlushAsync(CancellationToken)"/>
    public override Task FlushAsync(CancellationToken token)
        => FlushAsync(flushToDisk: false, token).AsTask();

    /// <inheritdoc/>
    public override int Read(Span<byte> data)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> data, CancellationToken token = default)
        => ValueTask.FromException<int>(new NotSupportedException());

    /// <inheritdoc/>
    public override void SetLength(long value)
        => throw new NotSupportedException();

    /// <summary>
    /// Drains the written content to the consumer asynchronously.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="consumer">The consumer of the written content.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public async Task CopyToAsync<TConsumer>(TConsumer consumer, int bufferSize, CancellationToken token)
        where TConsumer : ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

        if (fileBackend is not null)
        {
            using var buffer = allocator.AllocateAtLeast(bufferSize);
            int count;
            for (var offset = 0L; (count = await RandomAccess.ReadAsync(fileBackend, buffer.Memory, offset, token).ConfigureAwait(false)) > 0; offset += count)
                await consumer.Invoke(buffer.Memory.Slice(0, count), token).ConfigureAwait(false);
        }

        if (HasBufferedData)
            await consumer.Invoke(WrittenMemory, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    ValueTask IGrowableBuffer<byte>.CopyToAsync<TConsumer>(TConsumer consumer, CancellationToken token)
        => new(CopyToAsync(consumer, Options.DefaultFileBufferSize, token));

    /// <summary>
    /// Drains the written content to the consumer synchronously.
    /// </summary>
    /// <typeparam name="TConsumer">The type of the consumer.</typeparam>
    /// <param name="consumer">The consumer of the written content.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public void CopyTo<TConsumer>(TConsumer consumer, int bufferSize, CancellationToken token)
        where TConsumer : IReadOnlySpanConsumer<byte>
    {
        if (fileBackend is not null)
        {
            using var buffer = allocator.AllocateAtLeast(bufferSize);
            int count;
            for (var offset = 0L; (count = RandomAccess.Read(fileBackend, buffer.Span, offset)) > 0; offset += count, token.ThrowIfCancellationRequested())
                consumer.Invoke(buffer.Span.Slice(0, count));
        }

        if (HasBufferedData)
            consumer.Invoke(WrittenSpan);
    }

    /// <summary>
    /// Drains buffered content to the stream asynchronously.
    /// </summary>
    /// <param name="destination">The stream to drain buffered contents to.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous copy operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken token)
        => CopyToAsync<StreamConsumer>(destination, bufferSize, token);

    /// <summary>
    /// Drains buffered content to the stream synchronously.
    /// </summary>
    /// <param name="destination">The stream to drain buffered contents to.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    public override void CopyTo(Stream destination, int bufferSize)
    {
        ValidateCopyToArguments(destination, bufferSize);
        CopyTo<StreamConsumer>(destination, bufferSize, CancellationToken.None);
    }

    /// <summary>
    /// Drains buffered content to the buffer asynchronously.
    /// </summary>
    /// <param name="destination">The buffer to drain buffered contents to.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <returns>The task representing asynchronous execution of this method.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task CopyToAsync(IBufferWriter<byte> destination, int bufferSize = 1024, CancellationToken token = default)
        => CopyToAsync(new BufferConsumer<byte>(destination), bufferSize, token);

    /// <summary>
    /// Drains buffered content to the buffer synchronously.
    /// </summary>
    /// <param name="destination">The buffer to drain buffered contents to.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token to monitor for cancellation requests.</param>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public void CopyTo(IBufferWriter<byte> destination, int bufferSize = 1024, CancellationToken token = default)
        => CopyTo(new BufferConsumer<byte>(destination), bufferSize, token);

    /// <summary>
    /// Drains buffered content synchronously.
    /// </summary>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public void CopyTo<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = 1024, CancellationToken token = default)
        => CopyTo(new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), bufferSize, token);

    /// <summary>
    /// Drains buffered content asynchronously.
    /// </summary>
    /// <param name="reader">The content reader.</param>
    /// <param name="arg">The argument to be passed to the callback.</param>
    /// <param name="bufferSize">The size, in bytes, of the buffer used to copy bytes.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TArg">The type of the argument to be passed to the callback.</typeparam>
    /// <returns>The task representing asynchronous execution of the operation.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public Task CopyToAsync<TArg>(ReadOnlySpanAction<byte, TArg> reader, TArg arg, int bufferSize = 1024, CancellationToken token = default)
        => CopyToAsync(new DelegatingReadOnlySpanConsumer<byte, TArg>(reader, arg), bufferSize, token);

    /// <summary>
    /// Drains buffered content to the memory block synchronously.
    /// </summary>
    /// <param name="output">The memory block used as a destination for copy operation.</param>
    /// <returns>The actual number of copied elements.</returns>
    public int CopyTo(Span<byte> output)
    {
        var totalBytes = 0;

        if (output.IsEmpty)
            goto exit;

        if (fileBackend is not null)
        {
            totalBytes = RandomAccess.Read(fileBackend, output, 0L);
            output = output.Slice(totalBytes);
        }

        if (HasBufferedData)
        {
            WrittenSpan.CopyTo(output, out var subCount);
            totalBytes += subCount;
        }

    exit:
        return totalBytes;
    }

    /// <summary>
    /// Drains buffered content to the memory block asynchronously.
    /// </summary>
    /// <param name="output">The memory block used as a destination for copy operation.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The actual number of copied elements.</returns>
    public async ValueTask<int> CopyToAsync(Memory<byte> output, CancellationToken token = default)
    {
        var totalBytes = 0;

        if (output.IsEmpty)
            goto exit;

        if (fileBackend is not null)
        {
            totalBytes = await RandomAccess.ReadAsync(fileBackend, output, 0L, token).ConfigureAwait(false);
            output = output.Slice(totalBytes);
        }

        if (HasBufferedData)
        {
            WrittenSpan.CopyTo(output.Span, out var subCount);
            totalBytes += subCount;
        }

    exit:
        return totalBytes;
    }

    private static (long Offset, long Length) GetOffsetAndLength(in Range range, long length)
    {
        long start = range.Start.Value;
        if (range.Start.IsFromEnd)
            start = length - start;

        long end = range.End.Value;
        if (range.End.IsFromEnd)
            end = length - end;

        return (start, end - start);
    }

    /// <summary>
    /// Returns buffered content as a source of <see cref="Memory{T}"/> instances synchronously.
    /// </summary>
    /// <param name="range">The range of buffered content to return.</param>
    /// <returns>The memory manager providing access to buffered content.</returns>
    /// <exception cref="InvalidOperationException">The memory manager is already obtained but not disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is invalid.</exception>
    /// <exception cref="OutOfMemoryException">The size of buffered content is too large and cannot be represented by <see cref="Memory{T}"/> instance.</exception>
    public IMemoryOwner<byte> GetWrittenContent(Range range)
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        if (fileBackend is null)
            return new BufferedMemoryManager(this, range);

        if (HasBufferedData)
            PersistBuffer(flushToDisk: false);

        var (offset, length) = GetOffsetAndLength(range, filePosition);
        switch ((offset, length))
        {
            case (< 0L, _):
            case (_, < 0L):
                throw new ArgumentOutOfRangeException(nameof(range));
            case (0L, 0L):
                return new BufferedMemoryManager();
            case (_, > int.MaxValue):
                throw new InsufficientMemoryException();
        }

        var result = new NativeMemoryManager(this, unchecked((int)length));
        try
        {
            result.SetLength(RandomAccess.Read(fileBackend, result.GetSpan(), offset));
        }
        catch
        {
            result.As<IDisposable>().Dispose();
            throw;
        }

        return result;
    }

    /// <summary>
    /// Returns the whole buffered content as a source of <see cref="Memory{T}"/> instances synchronously.
    /// </summary>
    /// <remarks>
    /// Use <see cref="GetWrittenContent(Range)"/> if buffered content is too large.
    /// </remarks>
    /// <returns>The memory manager providing access to buffered content.</returns>
    /// <exception cref="InvalidOperationException">The memory manager is already obtained but not disposed.</exception>
    /// <exception cref="OutOfMemoryException">The size of buffered content is too large and cannot be represented by <see cref="Memory{T}"/> instance.</exception>
    public IMemoryOwner<byte> GetWrittenContent()
        => GetWrittenContent(Range.All);

    /// <summary>
    /// Returns buffered content as a source of <see cref="Memory{T}"/> instances asynchronously.
    /// </summary>
    /// <param name="range">The range of buffered content to return.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The memory manager providing access to buffered content.</returns>
    /// <exception cref="InvalidOperationException">The memory manager is already obtained but not disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> is invalid.</exception>
    /// <exception cref="OutOfMemoryException">The size of buffered content is too large and cannot be represented by <see cref="Memory{T}"/> instance.</exception>
    public async ValueTask<IMemoryOwner<byte>> GetWrittenContentAsync(Range range, CancellationToken token = default)
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        if (fileBackend is null)
            return new BufferedMemoryManager(this, range);

        if (HasBufferedData)
            await PersistBufferAsync(flushToDisk: false, token).ConfigureAwait(false);

        var (offset, length) = GetOffsetAndLength(range, filePosition);
        switch ((offset, length))
        {
            case (< 0L, _):
            case (_, < 0L):
                throw new ArgumentOutOfRangeException(nameof(range));
            case (0L, 0L):
                return new BufferedMemoryManager();
            case (_, > int.MaxValue):
                throw new InsufficientMemoryException();
        }

        var result = new NativeMemoryManager(this, unchecked((int)length));
        try
        {
            result.SetLength(await RandomAccess.ReadAsync(fileBackend, result.Memory, offset, token).ConfigureAwait(false));
        }
        catch
        {
            result.As<IDisposable>().Dispose();
            throw;
        }

        return result;
    }

    /// <summary>
    /// Returns the whole buffered content as a source of <see cref="Memory{T}"/> instances asynchronously.
    /// </summary>
    /// <remarks>
    /// Use <see cref="GetWrittenContentAsync(Range, CancellationToken)"/> if buffered content is too large.
    /// </remarks>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The memory manager providing access to buffered content.</returns>
    /// <exception cref="InvalidOperationException">The memory manager is already obtained but not disposed.</exception>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="OutOfMemoryException">The size of buffered content is too large and cannot be represented by <see cref="Memory{T}"/> instance.</exception>
    public ValueTask<IMemoryOwner<byte>> GetWrittenContentAsync(CancellationToken token = default)
        => GetWrittenContentAsync(Range.All, token);

    /// <summary>
    /// Attempts to get written content if it is located in memory.
    /// </summary>
    /// <remarks>
    /// If this method returns <see langword="false"/> then
    /// use <see cref="GetWrittenContent()"/>, <see cref="GetWrittenContent(Range)"/>,
    /// <see cref="GetWrittenContentAsync(CancellationToken)"/> or <see cref="GetWrittenContentAsync(Range, CancellationToken)"/>
    /// to obtain the content.
    /// </remarks>
    /// <param name="content">The written content.</param>
    /// <returns><see langword="true"/> if whole content is in memory and available without allocation of <see cref="MemoryManager{T}"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetWrittenContent(out ReadOnlyMemory<byte> content)
    {
        if (fileBackend is null)
        {
            content = WrittenMemory;
            return true;
        }

        content = default;
        return false;
    }

    /// <summary>
    /// Attempts to get written content if it is located in memory.
    /// </summary>
    /// <remarks>
    /// If this method returns <see langword="false"/> then
    /// <paramref name="fileName"/> contains full path to the file containing the written content.
    /// This method is useful only if the file was not created as temporary file.
    /// </remarks>
    /// <param name="content">The written content.</param>
    /// <param name="fileName">The path to the file used as a buffer if this writer switched to the file.</param>
    /// <returns><see langword="true"/> if whole content is in memory and available without allocation of <see cref="MemoryManager{T}"/>; otherwise, <see langword="false"/>.</returns>
    public bool TryGetWrittenContent(out ReadOnlyMemory<byte> content, [NotNullWhen(false)] out string? fileName)
    {
        if ((fileName = this.fileName) is null)
        {
            content = WrittenMemory;
            return true;
        }

        content = default;
        return false;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearCore(reuseBuffer: false);
            reader = null;
        }

        base.Dispose(disposing);
    }
}