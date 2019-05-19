using System;
using System.Linq;
using Xunit;

namespace DotNext
{
    public sealed class EnumerableTupleTests : Assert
    {
        [Fact]
        public static void TupleWithSingleItem()
        {
            var array = new ValueTuple<decimal>(10M).AsEnumerable();
            Single(array);
            Contains(10M, array);
        }

        [Fact]
        public static void TupleWithTwoItems()
        {
            var array = (10M, 20M).AsEnumerable();
            Equal(2, array.Count);
            Contains(10M, array);
            Contains(20M, array);
        }

        [Fact]
        public static void TupleWithThreeItems()
        {
            var array = (10M, 20M, 30M).AsEnumerable();
            Equal(3, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
        }

        [Fact]
        public static void TupleWithFourItems()
        {
            var array = (10M, 20M, 30M, 40M).AsEnumerable();
            Equal(4, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
        }

        [Fact]
        public static void TupleWithFiveItems()
        {
            var array = (10M, 20M, 30M, 40M, 50M).AsEnumerable();
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
            var array = (10M, 20M, 30M, 40M, 50M, 60M).AsEnumerable();
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
            var array = (10M, 20M, 30M, 40M, 50M, 60M, 70M).AsEnumerable();
            Equal(7, array.Count);
            Contains(10M, array);
            Contains(20M, array);
            Contains(30M, array);
            Contains(40M, array);
            Contains(50M, array);
            Contains(60M, array);
            Contains(70M, array);
        }
    }
}
