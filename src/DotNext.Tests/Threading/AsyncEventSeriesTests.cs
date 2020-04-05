using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AsyncEventSeriesTests : Test
    {
        [Fact]
        public static void InstantReturn()
        {
            using var series = new AsyncEventSeries<int>(0, Comparer<int>.Default);
            var task = series.WaitAsync(0, TimeSpan.Zero, CancellationToken.None);
            True(task.IsCompleted);
            True(task.Result);
            Equal(0, series.Instant);
        }

        private static int Advance(int x) => x + 1;

        private static void Advance(ref int x, int y) => x += y;

        [Fact]
        public static async Task WaitForEvent()
        {
            using var series = new AsyncEventSeries<int>(0, Comparer<int>.Default);
            var eventTask = series.WaitAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
            var valueTask = series.WaitAsync(42, TimeSpan.FromMinutes(1), CancellationToken.None);
            False(eventTask.IsCompleted);
            False(valueTask.IsCompleted);
            True(series.TryAdvance(10));
            Equal(10, series.Instant);
            True(await eventTask);
            False(valueTask.IsCompleted);
            True(series.TryAdvance(42));
            Equal(42, series.Instant);
            True(await valueTask);
        }

        [Fact]
        public static async Task WaitForEvent2()
        {
            var func = new ValueFunc<int, int>(Advance);
            using var series = new AsyncEventSeries<int>(40, Comparer<int>.Default);
            var eventTask = series.WaitAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
            var valueTask = series.WaitAsync(42, TimeSpan.FromMinutes(1), CancellationToken.None);
            False(eventTask.IsCompleted);
            False(valueTask.IsCompleted);
            Equal(41, series.TryAdvance(in func));
            Equal(41, series.Instant);
            True(await eventTask);
            False(valueTask.IsCompleted);
            Equal(42, series.TryAdvance(in func));
            True(await valueTask);
        }

        [Fact]
        public static async Task WaitForEvent3()
        {
            var func = new ValueRefAction<int, int>(Advance);
            using var series = new AsyncEventSeries<int>(40, Comparer<int>.Default);
            var eventTask = series.WaitAsync(TimeSpan.FromMinutes(1), CancellationToken.None);
            var valueTask = series.WaitAsync(42, TimeSpan.FromMinutes(1), CancellationToken.None);
            False(eventTask.IsCompleted);
            False(valueTask.IsCompleted);
            Equal(41, series.Advance(in func, 1));
            Equal(41, series.Instant);
            True(await eventTask);
            False(valueTask.IsCompleted);
            Equal(42, series.Advance(in func, 1));
            True(await valueTask);
        }
    }
}