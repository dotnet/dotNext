﻿using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Text;

namespace DotNext.IO;

using Buffers;
using PipeBinaryWriter = Pipelines.PipeBinaryWriter;

/// <summary>
/// Various extension methods for <see cref="IDataTransferObject"/>.
/// </summary>
public static class DataTransferObject
{
    private const int DefaultBufferSize = 1024;

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingDecoder<T> : IDataTransferObject.ITransformation<T>
    {
        private readonly Func<IAsyncBinaryReader, CancellationToken, ValueTask<T>> decoder;

        internal DelegatingDecoder(Func<IAsyncBinaryReader, CancellationToken, ValueTask<T>> decoder)
            => this.decoder = decoder;

        ValueTask<T> IDataTransferObject.ITransformation<T>.TransformAsync<TReader>(TReader reader, CancellationToken token)
            => decoder(reader, token);
    }

    // can return null if capacity == 0
    private static BufferWriter<byte>? CreateBuffer(long? capacity, MemoryAllocator<byte>? allocator) => capacity switch
    {
        null => new PoolingBufferWriter<byte>(allocator),
        0L => null,
        { } length when length <= Array.MaxLength => new PoolingBufferWriter<byte>(allocator) { Capacity = (int)length },
        _ => throw new InsufficientMemoryException(),
    };

    /// <summary>
    /// Copies the object content into the specified stream.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Transfer data object to transform.</param>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="buffer">The buffer to be used for transformation.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteToAsync<TObject>(this TObject dto, Stream output, Memory<byte> buffer, CancellationToken token = default)
        where TObject : IDataTransferObject
        => dto.WriteToAsync(new AsyncStreamBinaryAccessor(output, buffer), token);

    /// <summary>
    /// Copies the object content to the specified stream.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Transfer data object to transform.</param>
    /// <param name="output">The output stream receiving object content.</param>
    /// <param name="bufferSize">The size of the buffer to be used for transformation.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteToAsync<TObject>(this TObject dto, Stream output, int bufferSize = DefaultBufferSize, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        return dto.TryGetMemory(out var memory) ?
            output.WriteAsync(memory, token) :
            WriteToStreamAsync(dto, output, bufferSize, token);

        static async ValueTask WriteToStreamAsync(TObject dto, Stream output, int bufferSize, CancellationToken token)
        {
            using var buffer = Memory.AllocateAtLeast<byte>(bufferSize);
            await WriteToAsync(dto, output, buffer.Memory, token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Copies the object content to the specified pipe writer.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Transfer data object to transform.</param>
    /// <param name="output">The pipe writer receiving object content.</param>
    /// <param name="bufferSize">The maximum numbers of bytes that can be buffered in the memory without flushing.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteToAsync<TObject>(this TObject dto, PipeWriter output, long bufferSize = 0L, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        return bufferSize >= 0L
            ? dto.WriteToAsync(new PipeBinaryWriter(output, bufferSize), token)
            : ValueTask.FromException(new ArgumentOutOfRangeException(nameof(bufferSize)));
    }

    /// <summary>
    /// Copies the object content to the specified buffer.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Transfer data object to transform.</param>
    /// <param name="writer">The buffer writer.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The task representing state of asynchronous execution.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask WriteToAsync<TObject>(this TObject dto, IBufferWriter<byte> writer, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        ValueTask result;
        if (dto.TryGetMemory(out var memory))
        {
            result = new();
            try
            {
                writer.Write(memory.Span);
            }
            catch (Exception e)
            {
                result = ValueTask.FromException(e);
            }
        }
        else
        {
            result = dto.WriteToAsync(new AsyncBufferWriter(writer), token);
        }

        return result;
    }

    /// <summary>
    /// Converts DTO content to string.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Data transfer object to read from.</param>
    /// <param name="encoding">The encoding used to decode stored string.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The content of the object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<string> ToStringAsync<TObject>(this TObject dto, Encoding encoding, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        ValueTask<string> result;

        if (dto.TryGetMemory(out var memory))
        {
            try
            {
                result = new(memory.IsEmpty ? string.Empty : encoding.GetString(memory.Span));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<string>(e);
            }
        }
        else
        {
            result = DecodeAsync(dto, encoding, allocator, token);
        }

        return result;

        static async ValueTask<string> DecodeAsync(TObject dto, Encoding encoding, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            var buffer = CreateBuffer(dto.Length, allocator);
            if (buffer is null)
                return string.Empty;

            try
            {
                await dto.WriteToAsync(new AsyncBufferWriter(buffer), token).ConfigureAwait(false);
                return buffer.WrittenCount is 0 ? string.Empty : encoding.GetString(buffer.WrittenMemory.Span);
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts DTO to an array of bytes.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Data transfer object to read from.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The content of the object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<byte[]> ToByteArrayAsync<TObject>(this TObject dto, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        ValueTask<byte[]> result;

        if (dto.TryGetMemory(out var memory))
        {
            try
            {
                result = new(memory.ToArray());
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<byte[]>(e);
            }
        }
        else
        {
            result = BufferizeAsync(dto, allocator, token);
        }

        return result;

        static async ValueTask<byte[]> BufferizeAsync(TObject dto, MemoryAllocator<byte>? allocator, CancellationToken token)
        {
            var buffer = CreateBuffer(dto.Length, allocator);
            if (buffer is null)
                return [];

            try
            {
                await dto.WriteToAsync(new AsyncBufferWriter(buffer), token).ConfigureAwait(false);
                return buffer.WrittenMemory.ToArray();
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts DTO to a block of memory.
    /// </summary>
    /// <typeparam name="TObject">The type of data transfer object.</typeparam>
    /// <param name="dto">Data transfer object to read from.</param>
    /// <param name="allocator">The memory allocator.</param>
    /// <param name="token">The token that can be used to cancel asynchronous operation.</param>
    /// <returns>The content of the object.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<MemoryOwner<byte>> ToMemoryAsync<TObject>(this TObject dto, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        where TObject : IDataTransferObject
    {
        ValueTask<MemoryOwner<byte>> result;

        if (dto.TryGetMemory(out var memory))
        {
            try
            {
                result = new(memory.Span.Copy(allocator));
            }
            catch (Exception e)
            {
                result = ValueTask.FromException<MemoryOwner<byte>>(e);
            }
        }
        else
        {
            result = BufferizeAsync(dto, allocator, token);
        }

        return result;

        static async ValueTask<MemoryOwner<byte>> BufferizeAsync(TObject dto, MemoryAllocator<byte>? allocator = null, CancellationToken token = default)
        {
            var buffer = CreateBuffer(dto.Length, allocator);
            if (buffer is null)
                return new();

            try
            {
                await dto.WriteToAsync(new AsyncBufferWriter(buffer), token).ConfigureAwait(false);
                return buffer.DetachBuffer();
            }
            finally
            {
                buffer.Dispose();
            }
        }
    }

    /// <summary>
    /// Converts data transfer object to another type.
    /// </summary>
    /// <param name="dto">Data transfer object to decode.</param>
    /// <param name="transformation">The parser instance.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TResult">The type of result.</typeparam>
    /// <typeparam name="TObject">The type of the data transfer object.</typeparam>
    /// <returns>The converted DTO content.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    public static ValueTask<TResult> TransformAsync<TResult, TObject>(this TObject dto, Func<IAsyncBinaryReader, CancellationToken, ValueTask<TResult>> transformation, CancellationToken token = default)
        where TObject : IDataTransferObject
        => dto.TransformAsync<TResult, DelegatingDecoder<TResult>>(new DelegatingDecoder<TResult>(transformation), token);
}