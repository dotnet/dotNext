using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncResetEventTests: Assert
    {   
        [Fact]
        public static void ManualResetEvent()
        {
            using(var resetEvent = new AsyncResetEvent(false, EventResetMode.ManualReset))
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
            using (var mre = new AsyncResetEvent(false, EventResetMode.ManualReset))
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
        public static async Task SetResetForAutoEvent()
        {
            using (var are = new AsyncResetEvent(false, EventResetMode.AutoReset))
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