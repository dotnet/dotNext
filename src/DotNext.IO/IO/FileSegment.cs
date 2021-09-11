using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;
using SafeFileHandle = Microsoft.Win32.SafeHandles.SafeFileHandle;

namespace DotNext.IO
{
    /// <summary>
    /// Represents read-only view supporting random access over the portion of the file.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct FileSegment : IEquatable<FileSegment>
    {
        private readonly SafeFileHandle? handle;

        /// <summary>
        /// Initializes a new read-only view over the entire file.
        /// </summary>
        /// <param name="handle">The file descriptor.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        public FileSegment(SafeFileHandle handle)
        {
            ArgumentNullException.ThrowIfNull(handle, nameof(handle));

            Offset = 0L;
            var length = Length = RandomAccess.GetLength(handle);
            this.handle = length > 0L ? handle : null;
        }

        /// <summary>
        /// Initializes a new read-only view over the portion of the file.
        /// </summary>
        /// <param name="handle">The file descriptor.</param>
        /// <param name="offset">The starting position within the file.</param>
        /// <param name="length">The desired length of the segment.</param>
        /// <exception cref="ArgumentNullException"><paramref name="handle"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="offset"/> is less than zero;
        /// or <paramref name="length"/> is less than zero;
        /// or combination of <paramref name="length"/> and <paramref name="offset"/> is out of bounds.
        /// </exception>
        public FileSegment(SafeFileHandle handle, long offset, long length)
        {
            ArgumentNullException.ThrowIfNull(handle, nameof(handle));

            if (offset < 0L)
                throw new ArgumentNullException(nameof(offset));
            if (length < 0L)
                throw new ArgumentNullException(nameof(length));

            var realLength = RandomAccess.GetLength(handle);

            if (length == 0L || realLength == 0L)
            {
                this = default;
            }
            else if (offset + length > realLength)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            else
            {
                Length = length;
                Offset = offset;
                this.handle = handle;
            }
        }

        private FileSegment(in FileSegment source, long offset)
        {
            Debug.Assert(offset <= source.Length);

            if (offset == source.Length)
            {
                this = default;
            }
            else
            {
                Length = source.Length - offset;
                Offset = offset + source.Offset;
                this.handle = source.handle;
            }
        }

        private FileSegment(in FileSegment source, long offset, long length)
        {
            if (offset == source.Length || length == 0L)
            {
                this = default;
            }
            else
            {
                Offset = source.Offset + offset;
                Length = length;
                this.handle = source.handle;
            }
        }

        /// <summary>
        /// Gets starting position of the segment within the file.
        /// </summary>
        public long Offset { get; }

        /// <summary>
        /// Gets the length of the segment, in bytes.
        /// </summary>
        public long Length { get; }

        /// <summary>
        /// Gets a value indicating that this segment is empty.
        /// </summary>
        public bool IsEmpty => Length == 0L || handle is null;

        private long GetOffsetAndLength(ref long offset)
        {
            var result = Length - offset;
            offset += Offset;
            return result;
        }

        /// <summary>
        /// Reads the data from the file segment.
        /// </summary>
        /// <param name="output">The output buffer.</param>
        /// <param name="offset">The offset within the current segment.</param>
        /// <returns>The number of copied bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is less than zero.</exception>
        public int Read(Span<byte> output, long offset = 0L)
        {
            if (offset < 0L || offset > Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            var length = GetOffsetAndLength(ref offset);

            if (handle is null || output.IsEmpty)
                return 0;

            if (output.Length > length)
                output = output.Slice(length.Truncate());

            return RandomAccess.Read(handle, output, offset);
        }

        /// <summary>
        /// Reads the data from the file segment.
        /// </summary>
        /// <param name="output">The output buffer.</param>
        /// <param name="offset">The offset within the current segment.</param>
        /// <param name="token">The token that can be used to cancel the operation.</param>
        /// <returns>The number of copied bytes.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is less than zero.</exception>
        public ValueTask<int> Read(Memory<byte> output, long offset = 0L, CancellationToken token = default)
        {
            if ((ulong)offset > (ulong)Length)
                return ValueTask.FromException<int>(new ArgumentOutOfRangeException(nameof(offset)));

            var length = GetOffsetAndLength(ref offset);

            if (handle is null || output.IsEmpty)
                return new(0);

            if (output.Length > length)
                output = output.Slice(length.Truncate());

            return RandomAccess.ReadAsync(handle, output, offset, token);
        }

        /// <summary>
        /// Forms a slice out of the current segment that begins at a specified offset.
        /// </summary>
        /// <param name="offset">The offset within the current segment.</param>
        /// <returns>A new segment.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is less than zero or greater than <see cref="Length"/>.</exception>
        public FileSegment Slice(long offset)
        {
            if ((ulong)offset > (ulong)Length)
                throw new ArgumentOutOfRangeException(nameof(offset));

            return new(this, offset);
        }

        /// <summary>
        /// Forms a slice out of the current segment that begins at a specified offset for a specified length.
        /// </summary>
        /// <param name="offset">The offset within the current segment.</param>
        /// <param name="length">The desired length for the slice.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public FileSegment Slice(long offset, long length)
        {
            if ((ulong)offset + (ulong)length < (ulong)Length)
                throw new ArgumentOutOfRangeException();

            return new(this, offset, length);
        }

        private bool Equals(in FileSegment other)
            => ReferenceEquals(handle, other.handle) && Offset == other.Offset && Length == other.Length;

        /// <summary>
        /// Determines whether this object references the same file segment as other.
        /// </summary>
        /// <param name="other">The segment to compare.</param>
        /// <returns><see langword="true"/> if this object references the same file segment as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public bool Equals(FileSegment other) => Equals(in other);

        /// <summary>
        /// Determines whether this object references the same file segment as other.
        /// </summary>
        /// <param name="other">The segment to compare.</param>
        /// <returns><see langword="true"/> if this object references the same file segment as <paramref name="other"/>; otherwise, <see langword="false"/>.</returns>
        public override bool Equals([NotNullWhen(true)] object? other)
            => other is FileSegment segment && Equals(in segment);

        /// <summary>
        /// Computes identity hash code for the current segment.
        /// </summary>
        /// <remarks>
        /// This method doesn't take into account the actual file content.
        /// </remarks>
        /// <returns>The hash code.</returns>
        public override int GetHashCode()
        {
            var result = new HashCode();
            result.Add(RuntimeHelpers.GetHashCode(handle));
            result.Add(Offset);
            result.Add(Length);

            return result.ToHashCode();
        }

        /// <summary>
        /// Determines whether the two objects reference the same portion of the file.
        /// </summary>
        /// <param name="x">The first segment to compare.</param>
        /// <param name="y">The second segment to compare.</param>
        /// <returns><see langword="true"/> if both objects reference the same portion of the file; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(in FileSegment x, in FileSegment y)
            => x.Equals(in y);

        /// <summary>
        /// Determines whether the two objects reference the different portions of the file.
        /// </summary>
        /// <param name="x">The first segment to compare.</param>
        /// <param name="y">The second segment to compare.</param>
        /// <returns><see langword="true"/> if both objects reference the different portions of the file; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(in FileSegment x, in FileSegment y)
            => !x.Equals(in y);
    }
}