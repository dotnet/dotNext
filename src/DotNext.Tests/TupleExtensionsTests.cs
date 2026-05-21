namespace DotNext;

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
        Collection(array,
            static element => Equal(1, element),
            static element => Equal(2, element));
    }

    [Fact]
    public static void TupleToArray()
    {
        var array = Tuple.Create(1, 2).ToArray();
        Collection(array,
            static element => Equal(1, element),
            static element => Equal(2, element));
    }
}