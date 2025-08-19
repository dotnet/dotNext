using System.Net;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents multiplexing protocol exception.
/// </summary>
public abstract class MultiplexingProtocolException : ProtocolViolationException
{
    private protected MultiplexingProtocolException(string message)
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

/// <summary>
/// Indicates that the protocol version is not supported.
/// </summary>
public sealed class UnsupportedVersionException : MultiplexingProtocolException
{
    internal UnsupportedVersionException(byte version)
        : base(ExceptionMessages.BadProtocolVersion(version))
    {
    }
}