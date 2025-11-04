using System.Diagnostics;

namespace DotNext.IO;

/// <summary>
/// Represents a stream wrapper over the memory block.
/// </summary>
/// <param name="data">The mutable memory block.</param>
public sealed class MemorySegmentStream(Memory<byte> data) : ModernStream
{
    private int position, length = data.Length;

    /// <summary>
    /// Gets the consumed part of the data.
    /// </summary>
    public Span<byte> ConsumedSpan => data.Span[..position];

    /// <summary>
    /// Gets the remaining part of the data.
    /// </summary>
    public Span<byte> RemainingSpan => data.Span[position..length];

    /// <summary>
    /// Gets or sets a value indicating that <see cref="Write"/> and <see cref="WriteAsync"/> must throw
    /// <see cref="IOException"/> if the caller is trying to write past to the end of the underlying buffer.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>.
    /// </remarks>
    public bool SkipOnOverflow { get; init; }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken token)
        => token.IsCancellationRequested ? Task.FromCanceled(token) : Task.CompletedTask;

    [Conditional("DEBUG")]
    private void AssertState()
    {
        Debug.Assert(position <= length);
        Debug.Assert(length <= data.Length);
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        AssertState();
        
        RemainingSpan.CopyTo(buffer, out var count);
        position += count;
        return count;
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        ValueTask<int> task;
        if (token.IsCancellationRequested)
        {
            task = ValueTask.FromCanceled<int>(token);
        }
        else
        {
            try
            {
                task = new(Read(buffer.Span));
            }
            catch (Exception e)
            {
                task = ValueTask.FromException<int>(e);
            }
        }

        return task;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        AssertState();
        
        var newPosition = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => Position + offset,
            SeekOrigin.End => Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin))
        };

        if (newPosition < 0L)
            throw new IOException();

        ArgumentOutOfRangeException.ThrowIfGreaterThan(newPosition, Length, nameof(offset));

        position = (int)newPosition;
        return newPosition;
    }

    private void SetLength(int newLength)
    {
        if (newLength > data.Length)
            throw new IOException(ExceptionMessages.StreamOverflow);

        position = Math.Min(position, length = newLength);
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (uint)int.MaxValue, nameof(value));
        
        AssertState();
        SetLength((int)value);
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        AssertState();
        
        var remaining = RemainingSpan;
        if (remaining.Length >= buffer.Length)
        {
            // nothing to do
        }
        else if (SkipOnOverflow)
        {
            buffer = buffer.Slice(0, remaining.Length);
        }
        else
        {
            throw new IOException(ExceptionMessages.StreamOverflow);
        }

        buffer.CopyTo(remaining);
        position += buffer.Length;
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
    {
        ValueTask task;
        if (token.IsCancellationRequested)
        {
            task = ValueTask.FromCanceled(token);
        }
        else
        {
            task = ValueTask.CompletedTask;
            try
            {
                Write(buffer.Span);
            }
            catch (Exception e)
            {
                task = ValueTask.FromException(e);
            }
        }

        return task;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => true;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => length;

    /// <inheritdoc/>
    public override long Position
    {
        get => position;
        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan((ulong)value, (uint)length, nameof(value));

            position = (int)value;
            AssertState();
        }
    }
}