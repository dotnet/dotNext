using System;
using System.Runtime.InteropServices;

namespace DotNext.VariantType
{
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct Union<T1, T2>
        where T1: unmanaged
        where T2: unmanaged
    {
        [FieldOffset(0)]
        private readonly T1 first;
        [FieldOffset(0)]
        private readonly T2 second;

        public Union(T1 first)
        {
            second = default;
            this.first = first;
        }

        public Union(T2 second)
        {
            first = default;
            this.second = second;
        }
    }
}
