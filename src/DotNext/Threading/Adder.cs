using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal readonly struct Adder : ISupplier<double, double, double>, ISupplier<float, float, float>, ISupplier<nint, nint, nint>
{
    double ISupplier<double, double, double>.Invoke(double x, double y) => x + y;

    float ISupplier<float, float, float>.Invoke(float x, float y) => x + y;

    nint ISupplier<nint, nint, nint>.Invoke(nint x, nint y) => x + y;
}