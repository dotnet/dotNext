using System.Diagnostics.CodeAnalysis;

namespace DotNext.Generic;

using Threading.Tasks;

[ExcludeFromCodeCoverage]
public sealed class GenericConstTests : Test
{
    [Fact]
    public static void BooleanGenericConst()
    {
        False(CompletedTask<bool, BooleanConst.False>.Task.Result);
        True(CompletedTask<bool, BooleanConst.True>.Task.Result);
        var value = new BooleanConst.False();
        True(value.Equals(false));
        False(value.Equals(null));
        Equal(bool.FalseString, value.ToString());
        Equal(false.GetHashCode(), value.GetHashCode());
    }

    [Fact]
    public static void StringGenericConst()
    {
        Empty(CompletedTask<string, StringConst.Empty>.Task.Result);
        Null(CompletedTask<string, StringConst.Null>.Task.Result);
    }

    [Fact]
    public static void DefaultGenericConst()
    {
        Equal(0L, CompletedTask<long, DefaultConst<long>>.Task.Result);
        Null(CompletedTask<object, DefaultConst<object>>.Task.Result);
        Null(CompletedTask<int?, DefaultConst<int?>>.Task.Result);
    }

    [Fact]
    public static void IntGenericConst()
    {
        Equal(0, CompletedTask<int, Int32Const.Zero>.Task.Result);
        Equal(int.MaxValue, CompletedTask<int, Int32Const.Max>.Task.Result);
        Equal(int.MinValue, CompletedTask<int, Int32Const.Min>.Task.Result);
    }

    [Fact]
    public static void LongGenericConst()
    {
        Equal(0L, CompletedTask<long, Int64Const.Zero>.Task.Result);
        Equal(long.MaxValue, CompletedTask<long, Int64Const.Max>.Task.Result);
        Equal(long.MinValue, CompletedTask<long, Int64Const.Min>.Task.Result);
    }
}