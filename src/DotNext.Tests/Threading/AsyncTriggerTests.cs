using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncTriggerTests : Test
    {
        [Fact]
        public static void WaitForValue()
        {
            using var trigger = new AsyncTrigger<int>(0);
            var eventNode = trigger.WaitAsync();
            False(eventNode.IsCompleted);
            var valueNode = trigger.WaitAsync(i => i == 42);
            False(valueNode.IsCompleted);
            trigger.Signal();
            True(eventNode.IsCompletedSuccessfully);
            False(valueNode.IsCompleted);
            trigger.Signal(14);
            False(valueNode.IsCompleted);
            trigger.Signal(42);
            True(valueNode.IsCompletedSuccessfully);
        }

        private static void ModifyState(ref int state, int value) => state = value;

        private static int ModifyState(int state) => 42;

        [Fact]
        public static void WaitForValue2()
        {
            using var trigger = new AsyncTrigger<int>(0);
            var eventNode = trigger.WaitAsync();
            False(eventNode.IsCompleted);
            var valueNode = trigger.WaitAsync(i => i == 42);
            False(valueNode.IsCompleted);
            trigger.Signal();
            True(eventNode.IsCompletedSuccessfully);
            False(valueNode.IsCompleted);
            trigger.Signal(new ValueRefAction<int, int>(ModifyState), 14);
            False(valueNode.IsCompleted);
            trigger.Signal(new ValueRefAction<int, int>(ModifyState), 42);
            True(valueNode.IsCompletedSuccessfully);
        }

        [Fact]
        public static void WaitForValue3()
        {
            using var trigger = new AsyncTrigger<int>(0);
            var eventNode = trigger.WaitAsync();
            False(eventNode.IsCompleted);
            var valueNode = trigger.WaitAsync(i => i == 42);
            False(valueNode.IsCompleted);
            trigger.Signal();
            True(eventNode.IsCompletedSuccessfully);
            False(valueNode.IsCompleted);
            trigger.Signal(new ValueRefAction<int, int>(ModifyState), 42);
            True(valueNode.IsCompletedSuccessfully);
        }

        [Fact]
        public static void SignalAndWait()
        {
            using var trigger = new AsyncTrigger<int>(0);
            var waitTask = trigger.SignalAndWaitAsync(10, i => i == 42);
            False(waitTask.IsCompleted);
            Equal(10, trigger.CurrentState);
            trigger.Signal(42);
            True(waitTask.IsCompletedSuccessfully);
            Equal(42, trigger.CurrentState);
        }
    }
}