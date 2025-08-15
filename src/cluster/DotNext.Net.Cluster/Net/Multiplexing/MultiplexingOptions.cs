using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

using Buffers;

/// <summary>
/// Represents multiplexing protocol options.
/// </summary>
[Experimental("DOTNEXT001")]
public abstract class MultiplexingOptions
{
    private readonly int backlog = 10;
    private readonly PipeOptions options = PipeOptions.Default;

    /// <summary>
    /// Gets or sets buffering options.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public PipeOptions BufferOptions
    {
        get => options;
        init => options = value ?? throw new ArgumentNullException(nameof(value));
    }

    internal MemoryAllocator<byte> ToAllocator() => options.Pool.ToAllocator();

    internal int BufferCapacity
    {
        get
        {
            var minValue = MultiplexedStream.GetFrameSize(options) + FrameHeader.Size;
            var maxValue = options.PauseWriterThreshold * backlog;
            return int.Max(minValue, int.CreateSaturating(maxValue));
        }
    }

    /// <summary>
    /// Gets or sets measurement tags for metrics.
    /// </summary>
    public TagList MeasurementTags
    {
        get;
        init;
    }
    
    /// <summary>
    /// For the listener, it's the maximum amount of pending streams in the backlog.
    /// For the client, it's the maximum amount of the streams that can be in the batch for sending.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than or equal to zero.</exception>
    public int Backlog
    {
        get => backlog;
        init => backlog = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }
}