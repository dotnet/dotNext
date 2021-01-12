using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncTriggerTests : Test
    {
        private sealed class State : StrongBox<int>
        {

        }

        [Fact]
        public static void WaitForValue()
        {
            var state = new State { Value = 0 };
            using var trigger = new AsyncTrigger();
            var eventNode = trigger.WaitAsync();
            False(eventNode.IsCompleted);
            var valueNode = trigger.WaitAsync(state, i => i.Value == 42);
            False(valueNode.IsCompleted);
            trigger.Signal();
            True(eventNode.IsCompletedSuccessfully);
            False(valueNode.IsCompleted);
            state.Value = 14;
            trigger.Signal(state);
            False(valueNode.IsCompleted);
            state.Value = 42;
            trigger.Signal(state);
            True(valueNode.IsCompletedSuccessfully);
        }

        private static void ModifyState(State state, int value) => state.Value = value;

        private static void ModifyState(State state) => state.Value = 42;

        [Fact]
        public static void WaitForValue2()
        {
            var state = new State { Value = 0 };
            using var trigger = new AsyncTrigger();
            var eventNode = trigger.WaitAsync();
            False(eventNode.IsCompleted);
            var valueNode = trigger.WaitAsync(state, i => i.Value == 42);
            False(valueNode.IsCompleted);
            trigger.Signal();
            True(eventNode.IsCompletedSuccessfully);
            False(valueNode.IsCompleted);
            trigger.Signal(state, ModifyState, 14);
            Equal(14, state.Value);
            False(valueNode.IsCompleted);
            trigger.Signal(state, ModifyState);
            True(valueNode.IsCompletedSuccessfully);
            Equal(42, state.Value);
        }

        [Fact]
        public static void SignalAndWait()
        {
            var state = new State { Value = 10 };
            using var trigger = new AsyncTrigger();
            var waitTask = trigger.SignalAndWaitAsync(state, i => i.Value == 42);
            False(waitTask.IsCompleted);
            Equal(10, state.Value);
            state.Value = 42;
            trigger.Signal(state);
            True(waitTask.IsCompletedSuccessfully);
            Equal(42, state.Value);
        }

        [Fact]
        public static void SignalAndWaitUnconditionally()
        {
            using var trigger = new AsyncTrigger();
            var waitTask = trigger.SignalAndWaitAsync();
            False(waitTask.IsCompleted);
            var waitTask2 = trigger.SignalAndWaitAsync();
            True(waitTask.IsCompletedSuccessfully);
            False(waitTask2.IsCompleted);
            trigger.Signal();
            True(waitTask2.IsCompletedSuccessfully);
        }

        [Fact]
        public static void VariousStateTypes()
        {
            using var trigger = new AsyncTrigger();
            var untypedWait = trigger.WaitAsync();
            False(untypedWait.IsCompleted);
            var stringWait = trigger.WaitAsync(string.Empty, str => str.Length > 0);
            False(stringWait.IsCompleted);
            var arrayWait = trigger.WaitAsync(Array.Empty<int>(), array => array.Length > 0);
            False(arrayWait.IsCompleted);

            trigger.Signal("Hello, world!");
            True(untypedWait.IsCompletedSuccessfully);
            True(stringWait.IsCompletedSuccessfully);
            False(arrayWait.IsCompleted);

            trigger.Signal(new int[1]);
            True(arrayWait.IsCompletedSuccessfully);
        }
    }
}