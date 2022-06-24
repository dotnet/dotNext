using System.Buffers;
using System.CommandLine;
using System.CommandLine.IO;

namespace DotNext.Maintenance.CommandLine.IO;

/// <summary>
/// Represents standard error and output streams as buffers.
/// </summary>
public interface IManagementConsole : IConsole
{
    /// <summary>
    /// Gets the buffer writer for standard error.
    /// </summary>
    new IBufferWriter<char> Error { get; }

    /// <summary>
    /// Gets the buffer writer for standard output.
    /// </summary>
    new IBufferWriter<char> Out { get; }

    /// <inheritdoc />
    bool IStandardError.IsErrorRedirected => true;

    /// <inheritdoc />
    bool IStandardOut.IsOutputRedirected => true;

    /// <summary>
    /// Gets interaction session.
    /// </summary>
    IManagementSession Session { get; }
}