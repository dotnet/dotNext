using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    [Obsolete("A set of tests for backward compatibility")]
    public sealed class EnumerableTupleTests : Test
    {
        [Fact]
        public static void DefaultEnumerableTuple()
        {
            var enumerable = default(EnumerableTuple<decimal, ValueTuple>);
            Empty(enumerable);
            Throws<ArgumentOutOfRangeException>(() => enumerable[0]);
        }

        [Fact]
        public static void TupleWithSingleItem()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable(new ValueTuple<decimal>(10M));
            Single(array);
            Contains(10M, array);
            Equal(10M, array[0]);
            Throws<ArgumentOutOfRangeException>(() => array[1]);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal>(10M));
            Single(array);
            Contains(10M, array);
            Equal(10M, array[0]);
            Throws<ArgumentOutOfRangeException>(() => array[1]);
        }

        [Fact]
        public static void TupleWithTwoItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M));
            Equal(2, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Equal(10M, array[0]);
            Equal(20M, array[1]);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal>(10M, 20M));
            Equal(2, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Equal(10M, array[0]);
            Equal(20M, array[1]);
        }

        [Fact]
        public static void TupleWithThreeItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M, 30M));
            Equal(3, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Equal(10M, array[0]);
            Equal(20M, array[1]);
            Equal(30M, array[2]);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal, decimal>(10M, 20M, 30M));
            Equal(3, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Equal(10M, array[0]);
            Equal(20M, array[1]);
            Equal(30M, array[2]);
        }

        [Fact]
        public static void TupleWithFourItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M, 30M, 40M));
            Equal(4, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal, decimal, decimal>(10M, 20M, 30M, 40M));
            Equal(4, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
        }

        [Fact]
        public static void TupleWithFiveItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M, 30M, 40M, 50M));
            Equal(5, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal, decimal, decimal, decimal>(10M, 20M, 30M, 40M, 50M));
            Equal(5, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
        }

        [Fact]
        public static void TupleWithSixItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M, 30M, 40M, 50M, 60M));
            Equal(6, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
            Contains(60M, array);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal, decimal, decimal, decimal, decimal>(10M, 20M, 30M, 40M, 50M, 60M));
            Equal(6, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
            Contains(60M, array);
        }

        [Fact]
        public static void TupleWithSevenItems()
        {
            IReadOnlyList<decimal> array = EnumerableTuple.AsEnumerable((10M, 20M, 30M, 40M, 50M, 60M, 70M));
            Equal(7, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
            Contains(60M, array);
            Contains(70M, array);

            array = EnumerableTuple.AsEnumerable(new Tuple<decimal, decimal, decimal, decimal, decimal, decimal, decimal>(10M, 20M, 30M, 40M, 50M, 60M, 70M));
            Equal(7, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
            Contains(60M, array);
            Contains(70M, array);
        }

        [Fact]
        public static void CopyToMemory()
        {
            Span<decimal> values = new decimal[2];
            Equal(2, EnumerableTuple.AsEnumerable((10M, 20M, 30M)).CopyTo(values));
            Equal(10M, values[0]);
            Equal(20M, values[1]);
        }
    }
}
