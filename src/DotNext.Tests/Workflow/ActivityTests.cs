using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Workflow
{
    [ExcludeFromCodeCoverage]
    public sealed class ActivityTests : Test
    {
        private sealed class TestActivity : Activity<string>
        {
            private readonly StrongBox<string> result;

            internal TestActivity(StrongBox<string> result) => this.result = result;

            protected override async ActivityResult ExecuteAsync(ActivityContext<string> context)
            {
                True(context.Token.CanBeCanceled);

                await Task.Yield();
                DelayResult(context);
                True(await Checkpoint());
            }

            private void DelayResult(ActivityContext<string> context)
            {
                context.OnCheckpoint(() => result.Value = context.Input);
            }
        }

        [Fact]
        public static async void BasicFunctions()
        {
            var result = new StrongBox<string>();
            await using var engine = new InMemoryWorkflowEngine();
            engine.RegisterActivity<string, TestActivity>(() => new TestActivity(result));
            await engine.ExecuteAsync<string, TestActivity>("instance", "Hello, world!");
            Equal("Hello, world!", result.Value);
        }
    }
}