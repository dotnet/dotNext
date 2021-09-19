using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading.Tasks
{
    using Generic;

    [ExcludeFromCodeCoverage]
    public sealed class CompletedTaskTests : Test
    {
        [Fact]
        public static async Task CompletionTest()
        {
            var result = await CompletedTask<bool, BooleanConst.True>.Task;
            True(result);
            result = await CompletedTask<bool, BooleanConst.True>.Task;
            True(result);
            result = await CompletedTask<bool, BooleanConst.False>.Task;
            False(result);
        }
    }
}
