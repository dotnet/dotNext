using System;
using Xunit;

namespace DotNext
{
    public sealed class TupleExtensionsTests : Test
    {
        [Fact]
        public static void EmptyTupleToArray()
        {
            Empty(ValueTuple.Create().ToArray());
        }

        [Fact]
        public static void ValueTupleToArray()
        {
            var array = ValueTuple.Create(1, 2).ToArray();
            NotEmpty(array);
            Equal(1, array[0]);
            Equal(2, array[1]);
        }

        [Fact]
        public static void TupleToArray()
        {
            var array = Tuple.Create(1, 2).ToArray();
            NotEmpty(array);
            Equal(1, array[0]);
            Equal(2, array[1]);
        }
    }
}