using System;
using System.Threading.Tasks;
using System.IO;

namespace Cheats.Runtime.InteropServices
{
    [CLSCompliant(false)]
    public interface IUnmanagedMemory<T>: IDisposable
        where T: unmanaged
    {
        UnmanagedMemoryStream AsStream();

        unsafe T* Address { get; }

        ulong Size { get; }

        byte this[ulong offset]
        {
            get;
            set;
        }

        byte[] ToByteArray();

        void WriteTo(Stream destination);

        Task WriteToAsync(Stream destination);

        ulong WriteTo(byte[] destination);

        ulong ReadFrom(byte[] source);

        ulong ReadFrom(Stream source);

        Task<ulong> ReadFromAsync(Stream source);

        void ZeroMem();

        ReadOnlySpan<T> Span { get; }
    }
}