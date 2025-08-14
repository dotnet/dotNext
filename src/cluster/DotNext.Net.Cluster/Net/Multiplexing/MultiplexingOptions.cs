using System.Diagnostics;
using System.IO.Pipelines;

namespace DotNext.Net.Multiplexing;

using Buffers;

/// <summary>
/// Represents multiplexing protocol options.
/// </summary>
public abstract class MultiplexingOptions
{
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

    internal int SendBufferCapacity => int.CreateSaturating(options.PauseWriterThreshold);

    internal int FrameBufferSize => int.Max(MultiplexedStream.GetFrameSize(options) + FrameHeader.Size, SendBufferCapacity);
    
    /// <summary>
    /// Gets or sets measurement tags for metrics.
    /// </summary>
    public TagList MeasurementTags
    {
        get;
        init;
    }
}