using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncEventHubTests : Test
    {
        [Fact]
        public static void InvalidCount()
        {
            Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(0));
            Throws<ArgumentOutOfRangeException>(static () => new AsyncEventHub(-1));
        }

        [Fact]
        public static void WaitOne()
        {
            var hub = new AsyncEventHub(3);
            Equal(3, hub.Count);

            True(hub.Pulse(0));
            True(hub.WaitOneAsync(0).IsCompletedSuccessfully);
            False(hub.WaitOneAsync(1).IsCompleted);
        }

        [Fact]
        public static async Task WaitAny()
        {
            var hub = new AsyncEventHub(3);

            int[] indexes = { 0 };
            bool[] flags = { false };
            hub.Pulse(indexes, flags);
            True(flags[0]);

            Equal(0, await hub.WaitAnyAsync());
            Equal(0, await hub.WaitAnyAsync(new int[] { 0, 1 }));
        }

        [Fact]
        public static async Task WaitAny2()
        {
            var hub = new AsyncEventHub(3);

            int[] indexes = { 0 };
            bool[] flags = { false };
            hub.ResetAndPulse(indexes, flags);
            True(flags[0]);

            Equal(0, await hub.WaitAnyAsync());
            Equal(0, await hub.WaitAnyAsync(new int[] { 0, 1 }));
        }

        [Fact]
        public static async Task WaitAll()
        {
            var hub = new AsyncEventHub(3);

            Equal(3, hub.PulseAll());

            await hub.WaitAllAsync(new int[] { 0, 1 });
            await hub.WaitAllAsync();
        }

        [Fact]
        public static async Task WaitAll2()
        {
            var hub = new AsyncEventHub(3);

            bool[] flags = { false, false, false };
            hub.PulseAll(flags);
            True(flags[0]);
            True(flags[1]);
            True(flags[2]);

            await hub.WaitAllAsync(new int[] { 0, 1 });
            await hub.WaitAllAsync();
        }

        [Fact]
        public static void CaptureState()
        {
            var hub = new AsyncEventHub(3);
            Span<bool> state = stackalloc bool[hub.Count];
            state.Clear();

            hub.CaptureState(state);
            True(state.IndexOf(true) < 0);

            True(hub.Pulse(1));
            hub.CaptureState(state);
            Equal(1, state.IndexOf(true));
        }
    }
}