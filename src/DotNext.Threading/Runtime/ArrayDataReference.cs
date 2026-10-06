using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime;

[StructLayout(LayoutKind.Auto)]
internal readonly struct ArrayDataReference<T>(T[] array, nuint index) : ITypedReference<T>
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public ref T Value => ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), index);

    ref readonly T ITypedReference<T>.Value => ref Value;
}