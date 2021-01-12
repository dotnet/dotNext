using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Generic
{
    using Threading.Tasks;

    [ExcludeFromCodeCoverage]
    public sealed class GenericConstTests : Assert
    {
        [Fact]
        public static void BooleanGenericConst()
        {
            False(CompletedTask<bool, BooleanConst.False>.Task.Result);
            True(CompletedTask<bool, BooleanConst.True>.Task.Result);
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
            Equal(0, CompletedTask<int, IntConst.Zero>.Task.Result);
            Equal(int.MaxValue, CompletedTask<int, IntConst.Max>.Task.Result);
            Equal(int.MinValue, CompletedTask<int, IntConst.Min>.Task.Result);
        }

        [Fact]
        public static void LongGenericConst()
        {
            Equal(0L, CompletedTask<long, LongConst.Zero>.Task.Result);
            Equal(long.MaxValue, CompletedTask<long, LongConst.Max>.Task.Result);
            Equal(long.MinValue, CompletedTask<long, LongConst.Min>.Task.Result);
        }
    }
}
