using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DotNext.Workflow
{
    [ExcludeFromCodeCoverage]
    public sealed class ActivityTests : Test
    {
        private sealed class TestActivity : Activity
        {
            protected override async ActivityResult ExecuteAsync()
            {
                await Task.Delay(10);
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