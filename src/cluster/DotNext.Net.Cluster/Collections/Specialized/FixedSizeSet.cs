using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Specialized
{
    using Buffers;

    [StructLayout(LayoutKind.Auto)]
    internal struct FixedSizeSet<T> : IDisposable
    {
        [StructLayout(LayoutKind.Auto)]
        private readonly struct Node
        {
            internal readonly bool HasValue;
            internal readonly T Value;

            internal Node(T value)
            {
                Value = value;
                HasValue = true;
            }
        }

        private readonly ArrayRental<Node> buffer;
        private readonly IEqualityComparer<T> comparer;
        private readonly int size;
        private int count;

        internal FixedSizeSet(int size, IEqualityComparer<T>? comparer = null)
        {
            buffer = new ArrayRental<Node>(size * size, true);
            this.comparer = comparer ?? EqualityComparer<T>.Default;
            this.size = size;
            count = 0;
        }

        private int GetRowIndex(int hashCode) => (hashCode & int.MaxValue) % size * size;

        internal bool Add(T item)
        {
            if (size == 0)
                goto method_exit;
            var index = GetRowIndex(comparer.GetHashCode(item));
            ref Node lookup = ref buffer[index];
            for (var offset = 0; offset < size; offset++, lookup = ref Unsafe.Add(ref lookup, 1))
                if (lookup.HasValue)
                {
                    if (comparer.Equals(item, lookup.Value))
                        return false;
                }
                else if (count < size)
                {
                    lookup = new Node(item);
                    count += 1;
                    return true;
                }
                else
                    break;
            method_exit:
            throw new InvalidOperationException();
        }

        internal bool Contains(T item)
        {
            if (size == 0)
                goto method_exit;
            var index = GetRowIndex(comparer.GetHashCode(item));
            ref Node lookup = ref buffer.Span[index];
            for (var offset = 0; offset < size; offset++, lookup = ref Unsafe.Add(ref lookup, 1))
            {
                if (lookup.HasValue)
                    if (comparer.Equals(item, lookup.Value))
                        return true;
                    else
                        continue;
                break;
            }
            method_exit:
            return false;
        }

        public void Dispose()
        {
            buffer.Dispose();
            this = default;
        }
    }
}
