using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.MemoryMarshal;

namespace DotNext.IO
{
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

        private sealed unsafe class MemoryMappedFileManager : MemoryManager<byte>
        {
            private readonly MemoryMappedFile mappedFile;
            private readonly MemoryMappedViewAccessor accessor;
            private ReadSession session;
            private byte* ptr;

            internal MemoryMappedFileManager(FileBufferingWriter writer, long offset, long length)
            {
                Debug.Assert(length <= int.MaxValue);
                Debug.Assert(writer.fileBackend is not null);
                mappedFile = CreateMemoryMappedFile(writer.fileBackend);
                accessor = mappedFile.CreateViewAccessor(offset, length, MemoryMappedFileAccess.ReadWrite);
                accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref ptr);
                Debug.Assert(ptr != default);
                session = writer.EnterReadMode(this);
                Debug.Assert(writer.IsReading);
            }

            private void ThrowIfDisposed()
            {
                if (ptr == default)
                    throw new ObjectDisposedException(GetType().Name);
            }

            public override Span<byte> GetSpan()
            {
                ThrowIfDisposed();
                return new Span<byte>(ptr + accessor.PointerOffset, (int)accessor.Capacity);
            }

            public override Memory<byte> Memory => CreateMemory((int)accessor.Capacity);

            public override MemoryHandle Pin(int elementIndex)
            {
                ThrowIfDisposed();
                if (elementIndex < 0 || elementIndex >= accessor.Capacity)
                    throw new ArgumentOutOfRangeException(nameof(elementIndex));
                return new MemoryHandle(ptr + accessor.PointerOffset + elementIndex);
            }

            public override void Unpin() => ThrowIfDisposed();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                    accessor.Dispose();
                    mappedFile.Dispose();
                    session.Dispose();
                    session = default;
                }

                ptr = default;
            }
        }

        private sealed class MemoryManager : MemoryManager<byte>
        {
            private ReadSession session;
            private Memory<byte> memory;

            internal MemoryManager() => memory = default;

            internal MemoryManager(FileBufferingWriter writer, in Range range)
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
            {
                task.ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        private readonly BackingFileProvider fileProvider;
        private readonly int memoryThreshold;
        private readonly MemoryAllocator<byte>? allocator;
        private MemoryOwner<byte> buffer;
        private int position;
        private FileStream? fileBackend;
        private EventCounter? allocationCounter;

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
                buffer = allocator.Invoke(options.InitialCapacity, false);
                if (buffer.Length > memoryThreshold)
                    memoryThreshold = buffer.Length < int.MaxValue ? buffer.Length + 1 : int.MaxValue;
            }

            this.memoryThreshold = memoryThreshold;
            fileProvider = new BackingFileProvider(in options);
            allocationCounter = options.AllocationCounter;
        }

        /// <summary>
        /// Sets the counter used to report allocation of the internal buffer.
        /// </summary>
        [Obsolete("Use Options.AllocationCounter property instead")]
        public EventCounter? AllocationCounter
        {
            set => allocationCounter = value;
        }

        private static MemoryMappedFile CreateMemoryMappedFile(FileStream fileBackend)
            => MemoryMappedFile.CreateFromFile(fileBackend, null, fileBackend.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

        /// <inheritdoc/>
        public override bool CanRead => false;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => true;

        /// <inheritdoc/>
        public override bool CanTimeout => fileBackend?.CanTimeout ?? false;

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override long Length => position + (fileBackend?.Length ?? 0L);

        /// <inheritdoc />
        long IGrowableBuffer<byte>.WrittenCount => Length;

        /// <inheritdoc />
        void IGrowableBuffer<byte>.CopyTo<TConsumer>(TConsumer consumer)
            => CopyTo(consumer, 1024, CancellationToken.None);

        private bool IsReading => reader?.Target is not null;

        private ReadSession EnterReadMode(object obj)
        {
            WeakReference refHolder;
            if (reader is null)
            {
                refHolder = reader = new WeakReference(obj, false);
            }
            else
            {
                refHolder = reader;
                refHolder.Target = obj;
            }

            return new ReadSession(refHolder);
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
            buffer = default;
            fileBackend?.Dispose();
            fileBackend = null;
            position = 0;
        }

        /// <inheritdoc/>
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (sizeHint < 0)
                throw new ArgumentOutOfRangeException(nameof(sizeHint));
            if (sizeHint == 0)
                sizeHint = Math.Max(1, buffer.Length - position);

            switch (PrepareMemory(sizeHint, out var result))
            {
                case MemoryEvaluationResult.Success:
                    break;
                case MemoryEvaluationResult.PersistExistingBuffer:
                    PersistBuffer();
                    buffer = allocator.Invoke(sizeHint, false);
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
            var newSize = buffer.Length + size;
            if (newSize > memoryThreshold)
            {
                output = default;
                return size <= memoryThreshold ? MemoryEvaluationResult.PersistExistingBuffer : MemoryEvaluationResult.PersistAll;
            }

            if (buffer.Length - position < size)
            {
                var newBuffer = allocator.Invoke(newSize, false);
                buffer.Memory.CopyTo(newBuffer.Memory);
                buffer.Dispose();
                buffer = newBuffer;
                allocationCounter?.WriteMetric(newBuffer.Length);
            }

            output = buffer.Memory.Slice(position, size);
            return MemoryEvaluationResult.Success;
        }

        private async ValueTask PersistBufferAsync(CancellationToken token)
        {
            if (buffer.Length > 0 && position > 0)
            {
                EnsureBackingStore();
                Debug.Assert(fileBackend is not null);
                await fileBackend.WriteAsync(buffer.Memory.Slice(0, position), token).ConfigureAwait(false);
                buffer.Dispose();
                buffer = default;
                position = 0;
            }
        }

        private void PersistBuffer()
        {
            if (buffer.Length > 0 && position > 0)
            {
                EnsureBackingStore();
                Debug.Assert(fileBackend is not null);
                fileBackend.Write(buffer.Memory.Span.Slice(0, position));
                buffer.Dispose();
                buffer = default;
                position = 0;
            }
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            if (IsReading)
                throw new InvalidOperationException(ExceptionMessages.WriterInReadMode);

            switch (PrepareMemory(buffer.Length, out var output))
            {
                case MemoryEvaluationResult.Success:
                    token.ThrowIfCancellationRequested();
                    buffer.CopyTo(output);
                    position += buffer.Length;
                    break;
                case MemoryEvaluationResult.PersistExistingBuffer:
                    await PersistBufferAsync(token).ConfigureAwait(false);
                    this.buffer = allocator.Invoke(buffer.Length, false);
                    allocationCounter?.WriteMetric(this.buffer.Length);
                    buffer.CopyTo(this.buffer.Memory);
                    position = buffer.Length;
                    break;
                case MemoryEvaluationResult.PersistAll:
                    await PersistBufferAsync(token).ConfigureAwait(false);
                    EnsureBackingStore();
                    Debug.Assert(fileBackend is not null);
                    await fileBackend.WriteAsync(buffer, token).ConfigureAwait(false);
                    break;
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
                    PersistBuffer();
                    this.buffer = allocator.Invoke(buffer.Length, false);
                    allocationCounter?.WriteMetric(this.buffer.Length);
                    buffer.CopyTo(this.buffer.Memory.Span);
                    position = buffer.Length;
                    break;
                case MemoryEvaluationResult.PersistAll:
                    PersistBuffer();
                    EnsureBackingStore();
                    Debug.Assert(fileBackend is not null);
                    fileBackend.Write(buffer);
                    break;
            }
        }

        private void EnsureBackingStore() => fileBackend ??= fileProvider.Invoke();

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
            => Write(new ReadOnlySpan<byte>(buffer, offset, count));

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            => WriteAsync(new ReadOnlyMemory<byte>(buffer, offset, count), token).AsTask();

        /// <inheritdoc/>
        public override void WriteByte(byte value)
            => Write(CreateReadOnlySpan(ref value, 1));

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
                task.ConfigureAwait(false).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public override void EndWrite(IAsyncResult ar)
            => EndWrite((Task)ar);

        /// <inheritdoc/>
        public override void Flush()
            => fileBackend?.Flush();

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken token)
        {
            if (fileBackend is null)
                return token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;
            return fileBackend.FlushAsync(token);
        }

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
            => new ValueTask<int>(Task.FromException<int>(new NotSupportedException()));

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
                using var buffer = allocator.Invoke(bufferSize, false);
                fileBackend.Position = 0L;
                await fileBackend.CopyToAsync(consumer, buffer.Memory, token).ConfigureAwait(false);
            }

            if (buffer.Length > 0 && position > 0)
                await consumer.Invoke(buffer.Memory.Slice(0, position), token).ConfigureAwait(false);
        }

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
                using var buffer = allocator.Invoke(bufferSize, false);
                fileBackend.Position = 0L;
                fileBackend.CopyTo(consumer, buffer.Memory.Span, token);
            }

            if (buffer.Length > 0 && position > 0)
                consumer.Invoke(buffer.Memory.Span.Slice(0, position));
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
            => CopyTo<StreamConsumer>(destination, bufferSize, CancellationToken.None);

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
            if (fileBackend is not null)
            {
                var currentPos = fileBackend.Position;
                fileBackend.Position = 0L;
                totalBytes = fileBackend.Read(output);
                output = output.Slice(totalBytes);
                fileBackend.Position = currentPos;
            }

            if (buffer.Length > 0 && position > 0 && !output.IsEmpty)
            {
                buffer.Memory.Span.Slice(0, position).CopyTo(output, out var subCount);
                totalBytes += subCount;
            }

            return totalBytes;
        }

        /// <summary>
        /// Drains buffered content to the memory block asynchronously.
        /// </summary>
        /// <param name="output">The memory block used as a destination for copy operation.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of copied elements.</returns>
        public async Task<int> CopyToAsync(Memory<byte> output, CancellationToken token = default)
        {
            var totalBytes = 0;
            if (fileBackend is not null)
            {
                var currentPos = fileBackend.Position;
                fileBackend.Position = 0L;
                totalBytes = await fileBackend.ReadAsync(output, token).ConfigureAwait(false);
                output = output.Slice(totalBytes);
                fileBackend.Position = currentPos;
            }

            if (buffer.Length > 0 && position > 0 && !output.IsEmpty)
            {
                buffer.Memory.Span.Slice(0, position).CopyTo(output.Span, out var subCount);
                totalBytes += subCount;
            }

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
                return new MemoryManager(this, range);

            PersistBuffer();
            fileBackend.Flush(true);
            var (offset, length) = GetOffsetAndLength(range, fileBackend.Length);
            if (offset < 0L || length < 0L)
                throw new ArgumentOutOfRangeException(nameof(range));
            if (length == 0L && offset == 0L)
                return new MemoryManager();
            if (length > int.MaxValue)
                throw new InsufficientMemoryException();
            return new MemoryMappedFileManager(this, offset, length);
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
                return new MemoryManager(this, range);

            await PersistBufferAsync(token).ConfigureAwait(false);
            await fileBackend.FlushAsync(token).ConfigureAwait(false);
            var (offset, length) = GetOffsetAndLength(range, fileBackend.Length);
            if (offset < 0L || length < 0L)
                throw new ArgumentOutOfRangeException(nameof(range));
            if (length == 0L && offset == 0L)
                return new MemoryManager();
            if (length > int.MaxValue)
                throw new InsufficientMemoryException();
            return new MemoryMappedFileManager(this, offset, length);
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
            if (fileBackend is null)
            {
                content = buffer.Memory.Slice(0, position);
                fileName = null;
                return true;
            }

            content = default;
            fileName = fileBackend.Name;
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
                buffer.Dispose();
                buffer = default;
                reader = null;
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            if (fileBackend is not null)
            {
                await fileBackend.DisposeAsync().ConfigureAwait(false);
                fileBackend = null;
            }

            buffer.Dispose();
            buffer = default;
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}