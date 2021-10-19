using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal readonly struct Adder : ISupplier<double, double, double>, ISupplier<float, float, float>, ISupplier<nint, nint, nint>, ISupplier<int, int, int>, ISupplier<long, long, long>, ISupplier<uint, uint, uint>, ISupplier<ulong, ulong, ulong>
{
    double ISupplier<double, double, double>.Invoke(double x, double y) => x + y;

    float ISupplier<float, float, float>.Invoke(float x, float y) => x + y;

    nint ISupplier<nint, nint, nint>.Invoke(nint x, nint y) => x + y;

    int ISupplier<int, int, int>.Invoke(int x, int y) => x + y;

    long ISupplier<long, long, long>.Invoke(long x, long y) => x + y;

    uint ISupplier<uint, uint, uint>.Invoke(uint x, uint y) => x + y;

    ulong ISupplier<ulong, ulong, ulong>.Invoke(ulong x, ulong y) => x + y;
}