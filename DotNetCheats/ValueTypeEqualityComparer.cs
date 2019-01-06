using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Cheats.Reflection
{

    public sealed class ValueTypeEqualityComparer<T>: IEqualityComparer<T>
        where T: struct
    {
        private ValueTypeEqualityComparer()
        {
        }

        public static IEqualityComparer<T> Instance { get; } = typeof(T).IsPrimitive ? EqualityComparer<T>.Default.Upcast<IEqualityComparer<T>, EqualityComparer<T>>() : new ValueTypeEqualityComparer<T>();
    
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(T first, T second) => first.BitwiseEquals(second);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetHashCode(T obj) => obj.BitwiseHashCode();
    }
}