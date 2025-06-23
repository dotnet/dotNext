using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices;

partial struct Pointer<T>
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
        if (IsNull)
            ThrowNullPointerException();

        if (!destination.CanWrite)
            throw new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination));

        if (count > 0)
            WriteTo((byte*)value, checked(count * sizeof(T)), destination);

        static void WriteTo(byte* source, long length, Stream destination)
        {
            for (int count; length > 0L; length -= count, source += count)
            {
                count = int.CreateSaturating(length);
                destination.Write(new(source, count));
            }
        }
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
    public ValueTask WriteToAsync(Stream destination, long count, CancellationToken token = default)
    {
        if (IsNull)
            return ValueTask.FromException(new NullPointerException());

        if (!destination.CanWrite)
            return ValueTask.FromException(new ArgumentException(ExceptionMessages.StreamNotWritable, nameof(destination)));

        unsafe
        {
            return count is 0L ? ValueTask.CompletedTask : WriteToAsync(Address, checked(count * sizeof(T)), destination, token);
        }

        static async ValueTask WriteToAsync(nint source, long length, Stream destination, CancellationToken token)
        {
            for (int count; length > 0; length -= count, source += count)
            {
                count = int.CreateSaturating(length);
                var memory = new Buffers.UnmanagedMemory<byte>(source, count).Memory;
                await destination.WriteAsync(memory, token).ConfigureAwait(false);
            }
        }
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
        if (IsNull)
            ThrowNullPointerException();

        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (!source.CanRead)
            throw new ArgumentException(ExceptionMessages.StreamNotReadable, nameof(source));

        return count is 0L ? 0L : ReadFrom(source, (byte*)value, checked(sizeof(T) * count));

        static long ReadFrom(Stream source, byte* destination, long length)
        {
            var total = 0L;
            for (int bytesRead; length > 0L; total += bytesRead, length -= bytesRead)
            {
                if ((bytesRead = source.Read(new Span<byte>(destination + total, int.CreateSaturating(length)))) is 0)
                    break;
            }

            return total;
        }
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
    public ValueTask<long> ReadFromAsync(Stream source, long count, CancellationToken token = default)
    {
        ValueTask<long> result;

        if (count < 0L)
        {
            result = ValueTask.FromException<long>(new ArgumentOutOfRangeException(nameof(count)));
        }
        else if (IsNull)
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
            unsafe
            {
                result = ReadFromStreamAsync(source, Address, checked(sizeof(T) * count), token);
            }
        }

        return result;

        static async ValueTask<long> ReadFromStreamAsync(Stream source, nint destination, long length, CancellationToken token)
        {
            var total = 0L;
            for (int bytesRead; length > 0L; length -= bytesRead, destination += bytesRead, total += bytesRead)
            {
                var memory = new Buffers.UnmanagedMemory<byte>(destination, int.CreateSaturating(length)).Memory;
                if ((bytesRead = await source.ReadAsync(memory, token).ConfigureAwait(false)) is 0)
                    break;
            }

            return total;
        }
    }

    /// <summary>
    /// Returns representation of the memory identified by this pointer in the form of the stream.
    /// </summary>
    /// <remarks>
    /// This method returns <see cref="Stream"/> compatible over the memory identified by this pointer. No copying is performed.
    /// </remarks>
    /// <param name="count">The number of elements of type <typeparamref name="T"/> referenced by this memory.</param>
    /// <param name="access">The type of the access supported by the returned stream.</param>
    /// <returns>The stream representing the memory identified by this pointer.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is less than 0.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe Stream AsStream(long count, FileAccess access = FileAccess.ReadWrite)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (IsNull)
            return Stream.Null;

        count = checked(count * sizeof(T));
        return new UnmanagedMemoryStream((byte*)value, count, count, access);
    }
}