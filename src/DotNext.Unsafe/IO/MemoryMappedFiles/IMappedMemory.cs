﻿using System.Runtime.InteropServices;

namespace DotNext.IO.MemoryMappedFiles;

using Runtime.InteropServices;

/// <summary>
/// Represents segment of memory-mapped file.
/// </summary>
[CLSCompliant(false)]
public interface IMappedMemory : IUnmanagedMemory<byte>, IFlushable
{
    /// <summary>
    /// Gets the buffer over the mapped memory.
    /// </summary>
    SafeBuffer Buffer { get; }
}