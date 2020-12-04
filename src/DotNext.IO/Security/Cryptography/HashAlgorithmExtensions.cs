using System;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Security.Cryptography
{
    using static IO.Pipelines.PipeExtensions;

    /// <summary>
    /// Supports hashing of the content exposed by <see cref="PipeReader"/>.
    /// </summary>
    public static class HashAlgorithmExtensions
    {
        /// <summary>
        /// Computes the hash for the pipe.
        /// </summary>
        /// <param name="algorithm">The hash algorithm.</param>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="count">The number of bytes to add to the hash.</param>
        /// <param name="hash">The buffer used to write the final hash.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Length of <paramref name="hash"/> is not enough to place the final hash.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        /// <exception cref="System.IO.EndOfStreamException">Unexpected end of stream.</exception>
        [Obsolete("Use PipeExtensions.ComputeHashAsync extension method instead")]
        public static ValueTask ComputeHashAsync(this HashAlgorithm algorithm, PipeReader reader, int count, Memory<byte> hash, CancellationToken token = default)
            => reader.ComputeHashAsync(algorithm, count, hash, token);

        /// <summary>
        /// Computes the hash for the pipe.
        /// </summary>
        /// <param name="algorithm">The hash algorithm.</param>
        /// <param name="reader">The pipe reader.</param>
        /// <param name="hash">The buffer used to write the final hash.</param>
        /// <param name="token">The token that can be used to cancel operation.</param>
        /// <returns>The task representing asynchronous execution of this method.</returns>
        /// <exception cref="InvalidOperationException">Length of <paramref name="hash"/> is not enough to place the final hash.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        [Obsolete("Use PipeExtensions.ComputeHashAsync extension method instead")]
        public static ValueTask ComputeHashAsync(this HashAlgorithm algorithm, PipeReader reader, Memory<byte> hash, CancellationToken token = default)
            => reader.ComputeHashAsync(algorithm, null, hash, token);
    }
}