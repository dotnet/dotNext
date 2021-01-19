#if !NETCOREAPP3_1
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    using IO;

    public sealed class PinnedArrayTests : Test
    {
        private static void CheckEmptyArray(IUnmanagedArray<int> array)
        {
            Equal(0, array.Length);
            Equal(Array.Empty<int>(), array);
            Equal(Array.Empty<int>(), array.ToArray());
            Equal(Stream.Null, array.AsStream());
            True(array.BitwiseEquals(Array.Empty<int>()));
            Equal(0, array.BitwiseCompare(Array.Empty<int>()));
            Empty(array);
            True(array.Pointer.IsNull);
            array = (IUnmanagedArray<int>)array.Clone();
            Equal(0, array.Length);
            Equal(Array.Empty<int>(), array);
            Equal(Array.Empty<int>(), array.ToArray());
            Equal(Stream.Null, array.AsStream());
        }

        [Fact]
        public static void EmptyArray()
        {
            var def = new PinnedArray<int>();
            var empty = new PinnedArray<int>(0);
            CheckEmptyArray(def);
            CheckEmptyArray(empty);
            True(empty == def);
            False(empty != def);
            Equal(def.GetHashCode(), empty.GetHashCode());
            True(def.Equals(Array.Empty<int>()));
            True(def.Span.IsEmpty);
        }

        [Fact]
        public static void Cloning()
        {
            var array = new PinnedArray<int>(4);
            Equal(4, array.Length);
            array[0] = 10;
            var clone = array.Clone();
            Equal(10, clone[0]);
            array[0] = 20;
            Equal(10, clone[0]);
        }

        [Fact]
        public static void UnsafeAccess()
        {
            var array = new PinnedArray<int>(4);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            array[3] = 40;

            var ptr = array.Pointer;
            Equal(10, ptr.Value);
            ptr += 1;
            Equal(20, ptr.Value);
            ptr += 1;
            Equal(30, ptr.Value);
            ptr += 1;
            Equal(40, ptr.Value);
        }

        [Fact]
        public static void ListInterop()
        {
            IList<int> list = new PinnedArray<int>(4);
            NotEmpty(list);
            Equal(4, list.Count);
            list[0] = 10;
            list[1] = 20;
            list[2] = 30;
            list[3] = 40;
            True(list.IsReadOnly);
            Equal(2, list.IndexOf(30));
            True(list.Contains(30));
            False(list.Contains(99));

            var output = new int[4];
            list.CopyTo(output, 0);
            Equal(output[0], list[0]);
            Equal(output[1], list[1]);
            Equal(output[2], list[2]);
            Equal(output[3], list[3]);

            Throws<NotSupportedException>(() => list.Add(40));
            Throws<NotSupportedException>(() => list.Remove(10));
            Throws<NotSupportedException>(() => list.RemoveAt(0));
            Throws<NotSupportedException>(() => list.Insert(0, 20));
            Throws<NotSupportedException>(list.Clear);
        }

        [Fact]
        public static void ReadOnlyListInterop()
        {
            var array = new PinnedArray<int>(4);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            array[3] = 40;

            IReadOnlyList<int> list = array;
            NotEmpty(list);
            Equal(4, list.Count);
            Equal(array[0], list[0]);
            Equal(array[1], list[1]);
            Equal(array[2], list[2]);
            Equal(array[3], list[3]);
        }

        [Fact]
        public static void BitwiseComparison()
        {
            var array = new PinnedArray<int>(4);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            array[3] = 40;

            var other = (int[])array.Array.Clone();
            True(array.BitwiseEquals(other));
            Equal(0, array.BitwiseCompare(other));

            array[0] = 99;
            False(array.BitwiseEquals(other));
            True(array.BitwiseCompare(other) > 0);
        }

        [Fact]
        public static void StreamInterop()
        {
            var array = new PinnedArray<int>(4);
            array[0] = 10;
            array[1] = 20;
            array[2] = 30;
            array[3] = 40;

            using var ms = array.AsStream();
            Equal(10, ms.Read<int>());
            Equal(20, ms.Read<int>());
            Equal(30, ms.Read<int>());
            Equal(40, ms.Read<int>());
        }
    }
}
#endif