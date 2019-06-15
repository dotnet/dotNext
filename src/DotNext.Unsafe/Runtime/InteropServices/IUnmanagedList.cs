using System.Collections.Generic;

namespace DotNext.Runtime.InteropServices
{
    internal interface IUnmanagedList<T> : IUnmanagedMemory<T>, IReadOnlyList<T>
        where T : unmanaged
    {
    }
}