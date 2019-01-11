using System;
using System.Collections.Generic;

namespace DotNext.Reflection
{
    internal sealed class RuntimeTypeHandleEqualityComparer: IEqualityComparer<RuntimeTypeHandle>
    {
        internal static readonly RuntimeTypeHandleEqualityComparer Instance = new RuntimeTypeHandleEqualityComparer();

        private RuntimeTypeHandleEqualityComparer()
        {

        }

        public bool Equals(RuntimeTypeHandle first, RuntimeTypeHandle second) => first.Equals(second);

        public int GetHashCode(RuntimeTypeHandle handle) => handle.GetHashCode();
    }
}