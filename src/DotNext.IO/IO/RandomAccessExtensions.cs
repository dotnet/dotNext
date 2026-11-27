using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO;

/// <summary>
/// Extends <see cref="RandomAccess"/> class.
/// </summary>
public static class RandomAccessExtensions
{
    /// <summary>
    /// Extends <see cref="RandomAccess"/> class.
    /// </summary>
    extension(RandomAccess)
    {
        /// <summary>
        /// Reads bytes from the file at the specified offset until <paramref name="buffer"/> is filled.
        /// </summary>
        /// <remarks>
        /// In contrast to <see cref="RandomAccess.Read(SafeFileHandle, Span{byte}, long)"/>, this method
        /// doesn't return when a single read returns fewer bytes than requested.
        /// </remarks>
        /// <param name="handle">The file handle.</param>
        /// <param name="buffer">The buffer to be filled with the bytes from the file.</param>
        /// <param name="fileOffset">The file position to read from.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="fileOffset"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the file is reached before <paramref name="buffer"/> is filled.</exception>
        public static void ReadExactly(SafeFileHandle handle, Span<byte> buffer, long fileOffset)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);

            for (int count; !buffer.IsEmpty; fileOffset += count, buffer = buffer.Slice(count))
            {
                count = RandomAccess.Read(handle, buffer, fileOffset);
                if (count is 0)
                    throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Reads bytes from the file at the specified offset until <paramref name="buffer"/> is filled.
        /// </summary>
        /// <remarks>
        /// In contrast to <see cref="RandomAccess.ReadAsync(SafeFileHandle, Memory{byte}, long, CancellationToken)"/>, this method
        /// doesn't complete when a single read returns fewer bytes than requested.
        /// </remarks>
        /// <param name="handle">The file handle.</param>
        /// <param name="buffer">The buffer to be filled with the bytes from the file.</param>
        /// <param name="fileOffset">The file position to read from.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The task representing asynchronous execution of the operation.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="fileOffset"/> is negative.</exception>
        /// <exception cref="EndOfStreamException">The end of the file is reached before <paramref name="buffer"/> is filled.</exception>
        /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
        public static ValueTask ReadExactlyAsync(SafeFileHandle handle, Memory<byte> buffer, long fileOffset, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(handle);
            ArgumentOutOfRangeException.ThrowIfNegative(fileOffset);

            return buffer.IsEmpty
                ? ValueTask.CompletedTask
                : ReadExactlyCoreAsync(handle, buffer, fileOffset, token);
            
            static async ValueTask ReadExactlyCoreAsync(SafeFileHandle handle, Memory<byte> buffer, long fileOffset, CancellationToken token)
            {
                for (int count; !buffer.IsEmpty; fileOffset += count, buffer = buffer.Slice(count))
                {
                    count = await RandomAccess.ReadAsync(handle, buffer, fileOffset, token).ConfigureAwait(false);
                    if (count is 0)
                        throw new EndOfStreamException();
                }
            }
        }
    }
}
