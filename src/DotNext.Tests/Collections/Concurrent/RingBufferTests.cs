#if !NETCOREAPP3_1
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Collections.Concurrent
{
    public sealed class RingBufferTests : Test
    {
        [Fact]
        public static void CapacityCalculation()
        {
            var buffer = new RingBuffer<int>(56);
            Equal(64, buffer.Capacity);

            buffer = new RingBuffer<int>(127);
            Equal(127, buffer.Capacity);
        }

        [Fact]
        public static void BasicConsumeProduce()
        {
            var buffer = new RingBuffer<int>(4);

            // produce
            True(buffer.TryAdd(42));
            True(buffer.TryAdd(43));
            True(buffer.TryAdd(44));
            True(buffer.TryAdd(45));

            // consume
            Equal(42, buffer.TryRemove().Value);
            Equal(43, buffer.TryRemove().Value);
            Equal(44, buffer.TryRemove().Value);
            True(buffer.TryRemove(out var value));
            Equal(45, value);

            True(buffer.TryAdd(46));
        }

        [Fact]
        public static void CallConsumeProduceTwice()
        {
            var buffer = new RingBuffer<int>(4);

            True(buffer.TryAllocate(out var reservation));
            reservation.SetAndPublish(42);

            var raised = false;
            try
            {
                reservation.Publish();
            }
            catch (InvalidOperationException)
            {
                raised = true;
            }

            True(raised);

            True(buffer.TryGet(out var acquisition));
            Equal(42, acquisition.GetAndConsume());

            raised = false;
            try
            {
                acquisition.Consume();
            }
            catch (InvalidOperationException)
            {
                raised = true;
            }

            True(raised);
        }

        [Fact]
        public static void WrapPoint()
        {
            var buffer = new RingBuffer<int>(3);

            Equal(3, buffer.Capacity);
            True(buffer.TryAdd(10));
            True(buffer.TryAdd(20));
            True(buffer.TryAdd(30));
            False(buffer.TryAdd(40));

            True(buffer.TryRemove(out var result));
            Equal(10, result);

            True(buffer.TryAdd(40));

            True(buffer.TryRemove(out result));
            Equal(20, result);
            True(buffer.TryRemove(out result));
            Equal(30, result);

            True(buffer.TryAdd(50));

            True(buffer.TryRemove(out result));
            Equal(40, result);

            True(buffer.TryRemove(out result));
            Equal(50, result);

            False(buffer.TryRemove(out result));
        }

        [Fact]
        public static async Task BasicProduceConsumeAsync()
        {
            var buffer = new RingBuffer<int>(3);
            var consumer = Task.Run(async () =>
            {
                var reader = buffer.CreateReader();
                Equal(42, await reader.ReadAsync());
            });
            var producer = Task.Run(async () =>
            {
                var writer = buffer.CreateWriter();
                await writer.WriteAsync(42);
            });

            await Task.WhenAll(consumer, producer);
        }

        [Fact]
        public static async Task ProduceConsumeCompleteAsync()
        {
            var buffer = new RingBuffer<int>(3);
            var reader = buffer.CreateReader();

            var consumer = Task.Run(async () =>
            {
                Equal(42, await reader.ReadAsync());
            });

            var producer = Task.Run(async () =>
            {
                var writer = buffer.CreateWriter();
                await writer.WriteAsync(42);
                writer.Complete();
            });

            await Task.WhenAll(consumer, producer);
            True(reader.Completion.IsCompletedSuccessfully);
        }
    }
}
#endif