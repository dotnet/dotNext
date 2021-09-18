namespace DotNext.IO.MemoryMappedFiles;

using Runtime.InteropServices;

/// <summary>
/// Represents segment of memory-mapped file.
/// </summary>
public interface IMappedMemoryOwner : IUnmanagedMemory<byte>, IFlushable
{
}