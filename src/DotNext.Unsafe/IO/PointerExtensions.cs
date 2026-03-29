using System.Runtime.InteropServices;

namespace DotNext.IO;

using Buffers;
using Runtime.InteropServices;

/// <summary>
/// Extensions for <see cref="Pointer{T}"/> data type.
/// </summary>
public static class PointerExtensions
{
    /// <summary>
    /// Extends <see cref="Pointer{T}"/> type with methods to work with <see cref="Stream"/>.
    /// </summary>
    /// <param name="receiver">The pointer value.</param>
    /// <typeparam name="T">The pointer element.</typeparam>
    extension<T>(Pointer<T> receiver) where T : unmanaged
    {
        /// <summary>
        /// Copies bytes from the memory location identified by this pointer to the stream.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentException">The stream is not writable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        [CLSCompliant(false)]
        public unsafe void WriteTo(Stream destination, long count)
        {
            if (receiver.IsNull)
                NullPointerException.Throw();

            if (!destination.CanWrite)
                throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));

            if (count > 0)
                WriteTo(receiver, checked(count * sizeof(T)), destination);
        }

        /// <summary>
        /// Copies bytes from the memory location identified
        /// by this pointer to the stream asynchronously.
        /// </summary>
        /// <param name="destination">The destination stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task instance representing asynchronous state of the copying process.</returns>
        /// <exception cref="NullPointerException">This pointer is equal to zero.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="ArgumentException">The stream is not writable.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public unsafe ValueTask WriteToAsync(Stream destination, long count, CancellationToken token = default)
        {
            if (receiver.IsNull)
                return ValueTask.FromException(new NullPointerException());

            if (!destination.CanWrite)
                return ValueTask.FromException(new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination)));

            return count is 0L
                ? ValueTask.CompletedTask
                : WriteToAsync(receiver, checked(count * sizeof(T)), destination, token);
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        public unsafe long ReadFrom(Stream source, long count)
        {
            if (receiver.IsNull)
                NullPointerException.Throw();

            ArgumentOutOfRangeException.ThrowIfNegative(count);
            if (!source.CanRead)
                throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));

            return count is 0L
                ? 0L
                : ReadFrom(source, receiver, checked(sizeof(T) * count));
        }

        /// <summary>
        /// Copies bytes from the given stream to the memory location identified by this pointer asynchronously.
        /// </summary>
        /// <param name="source">The source stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> to be copied.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The actual number of copied elements.</returns>
        /// <exception cref="NullPointerException">This pointer is zero.</exception>
        /// <exception cref="ArgumentException">The stream is not readable.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than zero.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public unsafe ValueTask<long> ReadFromAsync(Stream source, long count, CancellationToken token = default)
        {
            ValueTask<long> result;

            if (count < 0L)
            {
                result = ValueTask.FromException<long>(new ArgumentOutOfRangeException(nameof(count)));
            }
            else if (receiver.IsNull)
            {
                result = ValueTask.FromException<long>(new NullPointerException());
            }
            else if (!source.CanRead)
            {
                result = ValueTask.FromException<long>(new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source)));
            }
            else if (count is 0L)
            {
                result = new(0L);
            }
            else
            {
                result = ReadFromStreamAsync(source, receiver, checked(sizeof(T) * count), token);
            }

            return result;
        }
    }

    /// <summary>
    /// Extends <see cref="Stream"/> class to work with <see cref="Pointer{T}"/> type.
    /// </summary>
    extension(Stream)
    {
        /// <summary>
        /// Returns representation of the memory identified by this pointer in the form of the stream.
        /// </summary>
        /// <remarks>
        /// This method returns <see cref="Stream"/> compatible over the memory identified by this pointer. No copying is performed.
        /// </remarks>
        /// <param name="source">The source pointer that points to the data to be wrapped by the stream.</param>
        /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this memory.</param>
        /// <param name="access">The type of the access supported by the returned stream.</param>
        /// <returns>The stream representing the memory identified by this pointer.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0.</exception>
        public static unsafe Stream Create<T>(Pointer<T> source, long count, FileAccess access = FileAccess.ReadWrite)
            where T : unmanaged
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);

            if (source.IsNull)
                return Stream.Null;

            count = checked(count * sizeof(T));
            return new UnmanagedMemoryStream((Pointer<byte>)source, count, count, access);
        }
    }
    
    private static void WriteTo(Pointer<byte> source, long length, Stream destination)
    {
        for (int count; length > 0L; length -= count, source += count)
        {
            count = int.CreateSaturating(length);
            destination.Write(source.AsSpan(count));
        }
    }
    
    private static async ValueTask WriteToAsync(Pointer<byte> source, long length, Stream destination, CancellationToken token)
    {
        for (int count; length > 0; length -= count, source += count)
        {
            count = int.CreateSaturating(length);
            Memory<byte> memory;
            unsafe
            {
                memory = MemoryMarshal.AsMemory<byte>(source, count);
            }
            
            await destination.WriteAsync(memory, token).ConfigureAwait(false);
        }
    }
    
    private static long ReadFrom(Stream source, Pointer<byte> destination, long length)
    {
        var total = 0L;
        for (int bytesRead; length > 0L; total += bytesRead, length -= bytesRead)
        {
            if ((bytesRead = source.Read((destination + total).AsSpan(int.CreateSaturating(length)))) is 0)
                break;
        }

        return total;
    }

    private static async ValueTask<long> ReadFromStreamAsync(Stream source, Pointer<byte> destination, long length, CancellationToken token)
    {
        var total = 0L;
        for (int bytesRead; length > 0L; length -= bytesRead, destination += bytesRead, total += bytesRead)
        {
            Memory<byte> memory;
            unsafe
            {
                memory = MemoryMarshal.AsMemory<byte>(destination, int.CreateSaturating(length));
            }

            if ((bytesRead = await source.ReadAsync(memory, token).ConfigureAwait(false)) is 0)
                break;
        }

        return total;
    }
}