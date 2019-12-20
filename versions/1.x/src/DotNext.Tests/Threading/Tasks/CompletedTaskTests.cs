using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading.Tasks
{
    using Generic;

    [ExcludeFromCodeCoverage]
    public sealed class CompletedTaskTests : Assert
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
