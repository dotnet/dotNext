using System.Net;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents multiplexing protocol exception.
/// </summary>
public abstract class MultiplexingProtocolException : ProtocolViolationException
{
    internal MultiplexingProtocolException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Indicates that the server rejects the stream because its backlog is full.
/// </summary>
public sealed class StreamRejectedException : MultiplexingProtocolException
{
    internal StreamRejectedException()
        : base(ExceptionMessages.StreamRejected)
    {
    }
}