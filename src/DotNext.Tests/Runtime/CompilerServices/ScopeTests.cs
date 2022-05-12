using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage]
    public sealed class ScopeTests : Test
    {
        [Fact]
        public static void ExecutionOrder()
        {
            var stack = new Stack<int>();

            using (var scope = new Scope())
            {
                scope.Defer(() => stack.Push(10));
                scope.Defer(() => stack.Push(20));
                scope.RegisterForDispose(new StringWriter());
            }

            Equal(20, stack.Pop());
            Equal(10, stack.Pop());
        }

        [Fact]
        public static async Task ExecutionOrderAsync()
        {
            var stack = new Stack<int>();

            await using (var scope = new Scope())
            {
                scope.Defer(async () =>
                {
                    await Task.Yield();
                    stack.Push(10);
                });

                scope.Defer(async () =>
                {
                    await Task.Yield();
                    stack.Push(20);
                });

                scope.RegisterForDisposeAsync(new StringWriter());
            }

            Equal(20, stack.Pop());
            Equal(10, stack.Pop());
        }
    }
}