using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.IO
{
    using EncodingContext = Text.EncodingContext;

    /// <summary>
    /// Providers a uniform way to encode the data.
    /// </summary>
    /// <seealso cref="IAsyncBinaryReader"/>
    public interface IAsyncBinaryWriter
    {
        /// <summary>
        /// Encodes value of blittable type.
        /// </summary>
        /// <param name="value">The value to encode.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <typeparam name="T">The type of the value to encode.</typeparam>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteAsync<T>(T value, CancellationToken token)
            where T : unmanaged;
        
        /// <summary>
        /// Encodes a block of memory.
        /// </summary>
        /// <param name="input">A block of memory.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        ValueTask WriteAsync(ReadOnlyMemory<byte> input, CancellationToken token);

        /// <summary>
        /// Encodes a block of characters using the specified encoding.
        /// </summary>
        /// <param name="chars">The characters to encode.</param>
        /// <param name="context">The context describing encoding of characters.</param>
        /// <param name="lengthFormat">String length encoding format; or <see langword="null"/> to prevent encoding of string length.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="lengthFormat"/> is invalid.</exception>
        ValueTask WriteAsync(ReadOnlySpan<char> chars, EncodingContext context, StringLengthEncoding? lengthFormat, CancellationToken token);

        /// <summary>
        /// Writes the content from the specified stream.
        /// </summary>
        /// <param name="input">The stream to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task CopyFromAsync(Stream input, CancellationToken token);

        /// <summary>
        /// Writes the content from the specified pipe.
        /// </summary>
        /// <param name="input">The pipe to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing state of asynchronous execution.</returns>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        Task CopyFromAsync(PipeReader input, CancellationToken token);
    }
}