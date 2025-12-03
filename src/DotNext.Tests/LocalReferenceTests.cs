namespace DotNext;

public sealed class LocalReferenceTests : Test
{
    [Fact]
    public static void Operators()
    {
        int x = 10, y = 20;

        var ref1 = new LocalReference<int>(ref x);
        var ref2 = ref1;
        False(ref1.IsEmpty);
        False(ref2.IsEmpty);
        True(ref1 == ref2);
        False(ref1 != ref2);
        Equal(10, ref1.Value);
        Equal(10, ref2.Value);
        ref1.Value = 42;
        Equal(42, x);

        ref2 = new(ref y);
        False(ref1 == ref2);
        True(ref1 != ref2);
    }

    [Fact]
    public static void ReadOnlyView()
    {
        var x = 10;

        ReadOnlyLocalReference<int> ref1 = new LocalReference<int>(ref x);
        ReadOnlyLocalReference<int> ref2 = ref1;
        False(ref1.IsEmpty);
        False(ref2.IsEmpty);
        True(ref1 == ref2);
        False(ref1 != ref2);
        Equal(10, ref1.Value);
        Equal(10, ref2.Value);
    }

    [Fact]
    public static void DefaultValues()
    {
        True(default(LocalReference<int>).IsEmpty);
        True(default(ReadOnlyLocalReference<int>).IsEmpty);
    }
}