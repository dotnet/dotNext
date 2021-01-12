using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncResetEventTests : Assert
    {
        [Fact]
        public static void ManualResetEvent()
        {
            using (var resetEvent = new AsyncManualResetEvent(false))
            {
                False(resetEvent.IsSet);
                var t = new Thread(() =>
                {
                    resetEvent.Wait().Wait();
                })
                {
                    IsBackground = true
                };
                t.Start();
                True(resetEvent.Set());
                t.Join();
                True(resetEvent.Reset());
                False(resetEvent.IsSet);
                t = new Thread(() =>
                {
                    resetEvent.Wait().Wait();
                })
                {
                    IsBackground = true
                };
                t.Start();
                True(resetEvent.Set());
                t.Join();
                True(resetEvent.IsSet);
            }
        }

        [Fact]
        public static async Task SetResetForManualEvent()
        {
            using (var mre = new AsyncManualResetEvent(false))
            {
                False(await mre.Wait(TimeSpan.Zero));
                True(mre.Set());
                True(await mre.Wait(TimeSpan.Zero));
                True(await mre.Wait(TimeSpan.Zero));
                False(mre.Set());
                True(await mre.Wait(TimeSpan.Zero));
                True(mre.Reset());
                False(await mre.Wait(TimeSpan.Zero));
            }
        }

        [Fact]
        public static async Task AutoresetForManualEvent()
        {
            using (var resetEvent = new AsyncManualResetEvent(false))
            {
                False(resetEvent.IsSet);
                var t = new Thread(() =>
                {
                    resetEvent.Wait().Wait();
                })
                {
                    IsBackground = true
                };
                t.Start();
                var spinner = new SpinWait();
                while ((t.ThreadState & ThreadState.WaitSleepJoin) == 0)
                    spinner.SpinOnce();
                True(resetEvent.Set(true));
                t.Join();
                False(resetEvent.IsSet);
                False(await resetEvent.Wait(TimeSpan.Zero));
            }
        }

        [Fact]
        public static void AutoresetOfSignaledManualEvent()
        {
            using (var resetEvent = new AsyncManualResetEvent(true))
            {
                True(resetEvent.IsSet);
                False(resetEvent.Set());
                False(resetEvent.Set(true));
                False(resetEvent.IsSet);
            }
        }

        [Fact]
        public static async Task SetResetForAutoEvent()
        {
            using (var are = new AsyncAutoResetEvent(false))
            {
                False(await are.Wait(TimeSpan.Zero));
                True(are.Set());
                True(await are.Wait(TimeSpan.Zero));
                False(await are.Wait(TimeSpan.Zero));
                True(are.Set());
                True(are.Reset());
                False(await are.Wait(TimeSpan.FromMilliseconds(100)));
            }
        }
    }
}