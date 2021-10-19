using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal readonly struct Increment : ISupplier<double, double>, ISupplier<float, float>, ISupplier<nint, nint>, ISupplier<nuint, nuint>
{
    double ISupplier<double, double>.Invoke(double value) => value + 1D;

    float ISupplier<float, float>.Invoke(float value) => value + 1F;

    nint ISupplier<nint, nint>.Invoke(nint value) => value + 1;

    nuint ISupplier<nuint, nuint>.Invoke(nuint value) => value + 1U;
}