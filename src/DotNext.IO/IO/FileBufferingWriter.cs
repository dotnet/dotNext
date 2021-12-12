using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

using Buffers;
using static Threading.AsyncDelegate;

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
public sealed partial class FileBufferingWriter : Stream, IBufferWriter<byte>, IGrowableBuffer<byte>, IFlushable
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
            Debug.Assert(length <= int.MaxValue);
            Debug.Assert(writer.fileBackend is not null);

            this.length = length;
            ptr = NativeMemory.Alloc((nuint)length);
            session = writer.EnterReadMode(this);

            Debug.Assert(writer.IsReading);
        }

        internal void SetLength(int value) => length = value;

        private void ThrowIfDisposed()
        {
            if (ptr == null)
                throw new ObjectDisposedException(GetType().Name);
        }

        public override Span<byte> GetSpan()
        {
            ThrowIfDisposed();
            return new(ptr, length);
        }

        public override Memory<byte> Memory => CreateMemory(length);

        public override MemoryHandle Pin(int elementIndex)
        {
            ThrowIfDisposed();
            return new(Unsafe.Add<byte>(ptr, elementIndex));
        }

        public override void Unpin() => ThrowIfDisposed();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                session.Dispose();
                session = default;
            }

            if (ptr != null)
                NativeMemory.Free(ptr);

            ptr = null;
            length = 0;
        }
    }

    private sealed class BufferedMemoryManager : MemoryManager<byte>
    {
        private ReadSession session;
        private Memory<byte> memory;

        internal BufferedMemoryManager() => memory = default;

        internal BufferedMemoryManager(FileBufferingWriter writer, in Range range)
        {
            var buffer = writer.buffer;
            memory = buffer.Memory.Slice(0, writer.position)[range];
            session = writer.EnterReadMode(this);
            Debug.Assert(writer.IsReading);
        }

        public override Span<byte> GetSpan()
            => memory.Span;

        public override Memory<byte> Memory => memory;

        public override MemoryHandle Pin(int elementIndex)
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

    private static readonly Action<Task, object?> Continuation;

    static FileBufferingWriter()
    {
        Continuation = OnCompleted;

        static void OnCompleted(Task task, object? state)
            => task.ConfigureAwait(false).GetAwaiter().GetResult();
    }

    private readonly BackingFileProvider fileProvider;
    private readonly int memoryThreshold;
    private readonly MemoryAllocator<byte>? allocator;
    private readonly EventCounter? allocationCounter;
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
            buffer = allocator.Invoke(options.InitialCapacity, exactSize: false);
            if (buffer.Length > memoryThreshold)
                memoryThreshold = buffer.Length < int.MaxValue ? buffer.Length + 1 : int.MaxValue;
        }

        this.memoryThreshold = memoryThreshold;
        fileProvider = new BackingFileProvider(in options);
        allocationCounter = options.AllocationCounter;
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

    private ReadSession EnterReadMode(object obj)
    {
        WeakReference refHolder;
        if (reader is null)
        {
            refHolder = reader = new(obj, trackResurrection: false);
        }
        else
        {
            refHolder = reader;
            refHolder.Target = obj;
        }

        return new(refHolder);
    }

    /// <summary>
    /// Removes all written data.
    /// </summary>
    /// <exception cref="InvalidOperationException">Attempt to cleanup this writer while reading.</exception>
    public void Clear()
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        buffer.Dispose();
        fileBackend?.Dispose();
        fileBackend = null;
        fileName = null;
        filePosition = 0L;
        position = 0;
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
                Debug.Assert(HasBufferedData);
                PersistBuffer();
                buffer = allocator.Invoke(sizeHint, exactSize: false);
                allocationCounter?.WriteMetric(buffer.Length);
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
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));
        if (position > buffer.Length - count)
            throw new InvalidOperationException();
        position += count;
    }

    private MemoryEvaluationResult PrepareMemory(int size, out Memory<byte> output)
    {
        var newSize = position + size;

        if ((uint)newSize > (uint)memoryThreshold)
        {
            output = default;
            return size <= memoryThreshold ? MemoryEvaluationResult.PersistExistingBuffer : MemoryEvaluationResult.PersistAll;
        }

        if (buffer.Length < newSize)
        {
            buffer.Resize(newSize, exactSize: false, allocator: allocator);
            allocationCounter?.WriteMetric(buffer.Length);
        }

        output = buffer.Memory.Slice(position, size);
        return MemoryEvaluationResult.Success;
    }

    private bool HasBufferedData => !buffer.IsEmpty && position > 0;

    [MemberNotNull(nameof(fileBackend))]
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private async ValueTask PersistBufferAsync(CancellationToken token)
    {
        Debug.Assert(position > 0);
        EnsureBackingStore();
        await RandomAccess.WriteAsync(fileBackend, buffer.Memory.Slice(0, position), filePosition, token).ConfigureAwait(false);
        buffer.Dispose();
        filePosition += position;
        position = 0;
    }

    [MemberNotNull(nameof(fileBackend))]
    private void PersistBuffer()
    {
        Debug.Assert(position > 0);
        EnsureBackingStore();
        RandomAccess.Write(fileBackend, buffer.Span.Slice(0, position), filePosition);
        buffer.Dispose();
        filePosition += position;
        position = 0;
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        if (IsReading)
            return ValueTask.FromException(new InvalidOperationException(ExceptionMessages.WriterInReadMode));

        switch (PrepareMemory(buffer.Length, out var output))
        {
            default:
                return ValueTask.CompletedTask;
            case MemoryEvaluationResult.Success:
                buffer.CopyTo(output);
                position += buffer.Length;
                goto default;
            case MemoryEvaluationResult.PersistExistingBuffer:
                Debug.Assert(HasBufferedData);
                return PersistExistingBufferAsync();
            case MemoryEvaluationResult.PersistAll:
                return PersistAllAsync();
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        async ValueTask PersistExistingBufferAsync()
        {
            await PersistBufferAsync(token).ConfigureAwait(false);
            this.buffer = allocator.Invoke(buffer.Length, exactSize: false);
            allocationCounter?.WriteMetric(this.buffer.Length);
            buffer.CopyTo(this.buffer.Memory);
            position = buffer.Length;
        }

        [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
        async ValueTask PersistAllAsync()
        {
            if (HasBufferedData)
                await PersistBufferAsync(token).ConfigureAwait(false);
            else
                EnsureBackingStore();

            await RandomAccess.WriteAsync(fileBackend, buffer, filePosition, token).ConfigureAwait(false);
            filePosition += buffer.Length;
        }
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        if (IsReading)
            throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

        switch (PrepareMemory(buffer.Length, out var output))
        {
            case MemoryEvaluationResult.Success:
                buffer.CopyTo(output.Span);
                position += buffer.Length;
                break;
            case MemoryEvaluationResult.PersistExistingBuffer:
                Debug.Assert(HasBufferedData);
                PersistBuffer();
                this.buffer = allocator.Invoke(buffer.Length, exactSize: false);
                allocationCounter?.WriteMetric(this.buffer.Length);
                buffer.CopyTo(this.buffer.Span);
                position = buffer.Length;
                break;
            case MemoryEvaluationResult.PersistAll:
                if (HasBufferedData)
                    PersistBuffer();
                else
                    EnsureBackingStore();

                RandomAccess.Write(fileBackend, buffer, filePosition);
                filePosition += buffer.Length;
                break;
        }
    }

    [MemberNotNull(nameof(fileBackend))]
    private void EnsureBackingStore() => fileBackend ??= fileProvider.CreateBackingFileHandle(position, out fileName);

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        ValidateBufferArguments(buffer, offset, count);
        Write(new ReadOnlySpan<byte>(buffer, offset, count));
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), token).AsTask();

    /// <inheritdoc/>
    public override void WriteByte(byte value)
        => Write(MemoryMarshal.CreateReadOnlySpan(ref value, 1));

    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        Task task;

        if (fileProvider.IsAsynchronous)
        {
            task = WriteAsync(buffer, offset, count, CancellationToken.None);

            // attach state only if it's necessary
            if (state is not null)
                task = task.ContinueWith(Continuation, state, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

            if (callback is not null)
            {
                if (task.IsCompleted)
                    callback(task);
                else
                    task.ConfigureAwait(false).GetAwaiter().OnCompleted(() => callback(task));
            }
        }
        else
        {
            // start synchronous write as separated task
            task = new Action<object?>(_ => Write(buffer, offset, count)).BeginInvoke(state, callback);
        }

        return task;
    }

    private static void EndWrite(Task task)
    {
        using (task)
        {
            task.Wait();
        }
    }

    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult ar)
        => EndWrite((Task)ar);

    /// <inheritdoc/>
    public override void Flush()
    {
        if (fileBackend is not null && HasBufferedData)
            PersistBuffer();
    }

    private ValueTask FlushCoreAsync(CancellationToken token)
        => fileBackend is null || !HasBufferedData ? ValueTask.CompletedTask : PersistBufferAsync(token);

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken token)
        => FlushCoreAsync(token).AsTask();

    /// <inheritdoc/>
    public override int ReadByte()
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        => Task.FromException<int>(new NotSupportedException());

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token)
        => ValueTask.FromException<int>(new NotSupportedException());

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
        => throw new NotSupportedException();

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult)
        => throw new InvalidOperationException();

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
        where TConsumer : notnull, ISupplier<ReadOnlyMemory<byte>, CancellationToken, ValueTask>
    {
        if (bufferSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(bufferSize));

        if (fileBackend is not null)
        {
            using var buffer = allocator.Invoke(bufferSize, exactSize: false);
            int count;
            for (long offset = 0L; (count = await RandomAccess.ReadAsync(fileBackend, buffer.Memory, offset, token).ConfigureAwait(false)) > 0; offset += count)
                await consumer.Invoke(buffer.Memory.Slice(0, count), token).ConfigureAwait(false);
        }

        if (HasBufferedData)
            await consumer.Invoke(buffer.Memory.Slice(0, position), token).ConfigureAwait(false);
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
        where TConsumer : notnull, IReadOnlySpanConsumer<byte>
    {
        if (fileBackend is not null)
        {
            using var buffer = allocator.Invoke(bufferSize, exactSize: false);
            int count;
            for (long offset = 0L; (count = RandomAccess.Read(fileBackend, buffer.Span, offset)) > 0; offset += count, token.ThrowIfCancellationRequested())
                consumer.Invoke(buffer.Span.Slice(0, count));
        }

        if (HasBufferedData)
            consumer.Invoke(buffer.Span.Slice(0, position));
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
            buffer.Span.Slice(0, position).CopyTo(output, out var subCount);
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
            totalBytes = await RandomAccess.ReadAsync(fileBackend, output, 0L).ConfigureAwait(false);
            output = output.Slice(totalBytes);
        }

        if (HasBufferedData)
        {
            buffer.Span.Slice(0, position).CopyTo(output.Span, out var subCount);
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
            PersistBuffer();

        var (offset, length) = GetOffsetAndLength(range, filePosition);
        if (offset < 0L || length < 0L)
            throw new ArgumentOutOfRangeException(nameof(range));
        if (length == 0L && offset == 0L)
            return new BufferedMemoryManager();
        if (length > int.MaxValue)
            throw new InsufficientMemoryException();

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
            await PersistBufferAsync(token).ConfigureAwait(false);

        var (offset, length) = GetOffsetAndLength(range, filePosition);
        if (offset < 0L || length < 0L)
            throw new ArgumentOutOfRangeException(nameof(range));
        if (length == 0L && offset == 0L)
            return new BufferedMemoryManager();
        if (length > int.MaxValue)
            throw new InsufficientMemoryException();

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
            content = buffer.Memory.Slice(0, position);
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
        if (this.fileName is null)
        {
            content = buffer.Memory.Slice(0, position);
            fileName = null;
            return true;
        }

        content = default;
        fileName = this.fileName;
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
            fileBackend?.Dispose();
            fileBackend = null;
            fileName = null;
            buffer.Dispose();
            reader = null;
        }

        base.Dispose(disposing);
    }
}