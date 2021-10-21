using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal readonly struct BitwiseOr : ISupplier<nint, nint, nint>
{
    nint ISupplier<nint, nint, nint>.Invoke(nint x, nint y) => x | y;
}