using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime
{
    [ExcludeFromCodeCoverage]
    public sealed class GCNotificationTests : Test
    {
        [Fact]
        public static async Task GCHookAsync()
        {
            var task1 = GCNotification.GCTriggered().WaitAsync(DefaultTimeout);
            var task2 = GCNotification.GCTriggered(2).WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced);
            await task1;
            await task2;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public static void GCHook(bool continueOnCapturedContext)
        {
            using var mre = new ManualResetEvent(false);
            using var registration = GCNotification.GCTriggered().Register<ManualResetEvent>(static (mre, info) => mre.Set(), mre, continueOnCapturedContext);
            GC.Collect(2, GCCollectionMode.Forced);
            mre.WaitOne(DefaultTimeout);
        }

        [Fact]
        public static async Task HeapCompactionAsync()
        {
            var task = GCNotification.HeapCompaction().WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
            await task;
        }

        [Fact]
        public static async Task OrOperator()
        {
            var task = (GCNotification.HeapFragmentation(0.8D) | GCNotification.GCTriggered()).WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced);
            await task;
        }

        [Fact]
        public static async Task XorOperator()
        {
            var task = (GCNotification.HeapFragmentation(0.8D) ^ GCNotification.GCTriggered()).WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced);
            await task;
        }

        [Fact]
        public static async Task AndOperator()
        {
            var task = (GCNotification.GCTriggered() & GCNotification.HeapCompaction()).WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced, blocking: false, compacting: true);
            await task;
        }

        [Fact]
        public static async Task NotOperator()
        {
            var task = (!GCNotification.GCTriggered().Negate()).WaitAsync(DefaultTimeout);
            GC.Collect(2, GCCollectionMode.Forced);
            await task;
        }
    }
}