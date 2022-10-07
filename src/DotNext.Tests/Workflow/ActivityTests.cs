using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DotNext.Workflow
{
    [ExcludeFromCodeCoverage]
    public sealed class ActivityTests : Test
    {
        private sealed class TestActivity : Activity<string>
        {
            protected override async ActivityResult<string> ExecuteAsync(IActivityContext<string> context, CancellationToken token)
            {
                await Task.Delay(10);
                return string.Empty;
            }
        }

        [Fact]
        public static void BasicFunctions()
        {
            var act = new TestActivity();
            act.ToString();
        }
    }
}