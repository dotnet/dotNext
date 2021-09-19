using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncResetEventTests : Test
    {
        [Fact]
        public static async Task ManualResetEvent()
        {
            using var resetEvent = new AsyncManualResetEvent(false);
            False(resetEvent.IsSet);
            var t = Task.Run(async () =>
            {
                True(await resetEvent.WaitAsync(DefaultTimeout));
            });

            True(resetEvent.Set());
            await t;
            True(resetEvent.Reset());
            False(resetEvent.IsSet);

            t = Task.Run(async () =>
            {
                True(await resetEvent.WaitAsync(DefaultTimeout));
            });

            True(resetEvent.Set());
            await t;
            True(resetEvent.IsSet);
        }

        [Fact]
        public static async Task SetResetForManualEvent()
        {
            using var mre = new AsyncManualResetEvent(false);
            False(await mre.WaitAsync(TimeSpan.Zero));
            True(mre.Set());
            True(await mre.WaitAsync(TimeSpan.Zero));
            True(await mre.WaitAsync(TimeSpan.Zero));
            False(mre.Set());
            True(await mre.WaitAsync(TimeSpan.Zero));
            True(mre.Reset());
            False(await mre.WaitAsync(TimeSpan.Zero));
        }

        [Fact]
        public static async Task AutoresetForManualEvent()
        {
            using var resetEvent = new AsyncManualResetEvent(false, 3);
            False(resetEvent.IsSet);
            var t = resetEvent.WaitAsync(DefaultTimeout);

            True(resetEvent.Set(true));
            await t;

            False(resetEvent.IsSet);
            False(await resetEvent.WaitAsync(TimeSpan.Zero));
        }

        [Fact]
        public static void AutoresetOfSignaledManualEvent()
        {
            using var resetEvent = new AsyncManualResetEvent(true);
            True(resetEvent.IsSet);
            False(resetEvent.Set());
            False(resetEvent.Set(true));
            False(resetEvent.IsSet);
        }

        [Fact]
        public static async Task SetResetForAutoEvent()
        {
            using var are = new AsyncAutoResetEvent(false);
            False(await are.WaitAsync(TimeSpan.Zero));
            True(are.Set());
            True(await are.WaitAsync(TimeSpan.Zero));
            False(await are.WaitAsync(TimeSpan.Zero));
            True(are.Set());
            True(are.Reset());
            False(await are.WaitAsync(TimeSpan.FromMilliseconds(100)));
        }
    }
}