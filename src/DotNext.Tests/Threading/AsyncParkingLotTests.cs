using System.Diagnostics;
using System.Diagnostics.Metrics;
using static System.Threading.Timeout;

namespace DotNext.Threading;

using State = AsyncParkingLot.State;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class AsyncParkingLotTests : Test
{
    [Fact]
    public static void InvalidArguments()
    {
        Throws<ArgumentOutOfRangeException>(static () => new AsyncParkingLot(0));
        Throws<ArgumentOutOfRangeException>(static () => new AsyncParkingLot(1, poolSizePerSlot: 0L));

        using var lot = new AsyncParkingLot(1);
        var states = new State[1];
        True(lot.ParkAsync(-1, ref states[0], token: TestToken).IsFaulted);
        True(lot.ParkAsync(-1, ref states[0], null, InfiniteTimeSpan, TestToken).IsFaulted);
        Throws<ArgumentOutOfRangeException>(() => lot.Unpark(-1, ref states[0]));
        Throws<InvalidOperationException>(() => lot.Unpark(0, ref states[0]));
        Equal(State.Free, states[0]);
    }

    [Fact]
    public static void CapacityIsPowerOfTwo()
    {
        using var lot = new AsyncParkingLot(5);
        Equal(8, lot.Capacity);
    }

    [Fact]
    public static void UncontendedLock()
    {
        using var lot = new AsyncParkingLot(1);
        var states = new State[1];

        True(lot.ParkAsync(0, ref states[0], token: TestToken).IsCompletedSuccessfully);
        Equal(State.Locked, states[0]);

        var task = lot.ParkAsync(0, ref states[0], null, TimeSpan.Zero, TestToken);
        True(task.IsCompleted);
        False(task.Result);

        lot.Unpark(0, ref states[0]);
        Equal(State.Free, states[0]);

        task = lot.ParkAsync(0, ref states[0], null, TimeSpan.Zero, TestToken);
        True(task.IsCompletedSuccessfully);
        True(task.Result);
    }

    [Fact]
    public static async Task HandOffInFifoOrder()
    {
        using var lot = new AsyncParkingLot(1);
        var states = new State[1];

        await lot.ParkAsync(0, ref states[0], token: TestToken);
        var w1 = lot.ParkAsync(0, ref states[0], token: TestToken).AsTask();
        var w2 = lot.ParkAsync(0, ref states[0], token: TestToken).AsTask();
        Equal(State.LockedWithWaiters, states[0]);

        lot.Unpark(0, ref states[0]);
        await w1.WaitAsync(DefaultTimeout, TestToken);
        False(w2.IsCompleted);
        Equal(State.LockedWithWaiters, states[0]);

        // the lock is handed off, so the newcomer cannot barge in
        var w3 = lot.ParkAsync(0, ref states[0], token: TestToken).AsTask();
        False(w3.IsCompleted);

        lot.Unpark(0, ref states[0]);
        await w2.WaitAsync(DefaultTimeout, TestToken);
        False(w3.IsCompleted);

        lot.Unpark(0, ref states[0]);
        await w3.WaitAsync(DefaultTimeout, TestToken);
        Equal(State.Locked, states[0]);

        lot.Unpark(0, ref states[0]);
        Equal(State.Free, states[0]);
    }

    [Fact]
    public static async Task PositionsInSameSlotAreIndependent()
    {
        // positions 1, 5 and 9 are mapped to the same slot
        using var lot = new AsyncParkingLot(4);
        var states = new State[10];

        await lot.ParkAsync(5, ref states[5], token: TestToken);
        await lot.ParkAsync(9, ref states[9], token: TestToken);
        True(lot.ParkAsync(1, ref states[1], token: TestToken).IsCompletedSuccessfully);

        var a = lot.ParkAsync(5, ref states[5], token: TestToken).AsTask();
        var b = lot.ParkAsync(9, ref states[9], token: TestToken).AsTask();
        var c = lot.ParkAsync(5, ref states[5], token: TestToken).AsTask();

        lot.Unpark(9, ref states[9]);
        await b.WaitAsync(DefaultTimeout, TestToken);
        False(a.IsCompleted);
        False(c.IsCompleted);
        Equal(State.Locked, states[9]);

        // the waiter for another position between the waiters must not hide the next one
        lot.Unpark(5, ref states[5]);
        await a.WaitAsync(DefaultTimeout, TestToken);
        False(c.IsCompleted);
        Equal(State.LockedWithWaiters, states[5]);

        lot.Unpark(5, ref states[5]);
        await c.WaitAsync(DefaultTimeout, TestToken);
        Equal(State.Locked, states[5]);

        lot.Unpark(5, ref states[5]);
        lot.Unpark(9, ref states[9]);
        lot.Unpark(1, ref states[1]);
        All(states, static s => Equal(State.Free, s));
    }

    [Fact]
    public static async Task UserDataDistinguishesLocks()
    {
        using var lot = new AsyncParkingLot(1);
        var states = new State[2];
        object first = new(), second = new();

        await lot.ParkAsync(0, ref states[0], first, TestToken);
        await lot.ParkAsync(0, ref states[1], second, TestToken);

        var w1 = lot.ParkAsync(0, ref states[0], first, TestToken).AsTask();
        var w2 = lot.ParkAsync(0, ref states[1], second, TestToken).AsTask();

        lot.Unpark(0, ref states[1], second);
        await w2.WaitAsync(DefaultTimeout, TestToken);
        False(w1.IsCompleted);

        lot.Unpark(0, ref states[0], first);
        await w1.WaitAsync(DefaultTimeout, TestToken);
    }

    [Fact]
    public static async Task TimeoutAndCancellation()
    {
        using var lot = new AsyncParkingLot(1);
        var states = new State[1];

        await lot.ParkAsync(0, ref states[0], token: TestToken);
        False(await lot.ParkAsync(0, ref states[0], null, TimeSpan.FromMilliseconds(10), TestToken));

        using (var cts = new CancellationTokenSource())
        {
            var task = lot.ParkAsync(0, ref states[0], token: cts.Token).AsTask();
            await cts.CancelAsync();
            await ThrowsAnyAsync<OperationCanceledException>(() => task);
        }

        // no live waiters, the state is corrected on release
        Equal(State.LockedWithWaiters, states[0]);
        lot.Unpark(0, ref states[0]);
        Equal(State.Free, states[0]);
    }

    [Fact]
    public static async Task HandOffSkipsCanceledWaiter()
    {
        using var lot = new AsyncParkingLot(1);
        var states = new State[1];
        using var cts = new CancellationTokenSource();

        await lot.ParkAsync(0, ref states[0], token: TestToken);

        // the canceled task is not consumed, so its node remains in the queue
        var canceled = lot.ParkAsync(0, ref states[0], token: cts.Token);
        await cts.CancelAsync();
        var waiter = lot.ParkAsync(0, ref states[0], token: TestToken).AsTask();

        lot.Unpark(0, ref states[0]);
        await waiter.WaitAsync(DefaultTimeout, TestToken);

        await ThrowsAnyAsync<OperationCanceledException>(canceled.AsTask);
        lot.Unpark(0, ref states[0]);
        Equal(State.Free, states[0]);
    }

    [Fact]
    public static async Task DisposeResumesWaiters()
    {
        var lot = new AsyncParkingLot(1);
        var states = new State[1];

        await lot.ParkAsync(0, ref states[0], token: TestToken);
        var w1 = lot.ParkAsync(0, ref states[0], token: TestToken).AsTask();
        var w2 = lot.ParkAsync(0, ref states[0], null, InfiniteTimeSpan, TestToken).AsTask();

        lot.Dispose();
        await ThrowsAsync<ObjectDisposedException>(() => w1);
        await ThrowsAsync<ObjectDisposedException>(() => w2);
        await ThrowsAsync<ObjectDisposedException>(lot.ParkAsync(0, ref states[0], token: TestToken).AsTask);

        // the owner is able to release the lock after disposal
        lot.Unpark(0, ref states[0]);
        Equal(State.Free, states[0]);
    }

    [Fact]
    public static async Task StressTest()
    {
        const int positions = 32;
        const int iterations = 10_000;
        var testId = Guid.NewGuid();

        long suspendedCallers = 0L, parkedCallers = 0L;
        using var listener = new MeterListener();
        listener.InstrumentPublished = static (instrument, listener) =>
        {
            if (instrument.Meter.Name is "DotNext.Threading.AsyncParkingLot")
                listener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<int>((_, measurement, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag is { Key: "test", Value: Guid id } && id == testId)
                {
                    Interlocked.Add(ref suspendedCallers, measurement);
                    if (measurement > 0)
                        Interlocked.Increment(ref parkedCallers);
                }
            }
        });
        listener.Start();

        // few slots for many positions to have the waiters of different positions in the same queue
        using var lot = new AsyncParkingLot(4, poolSizePerSlot: 4L) { MeasurementTags = new TagList { { "test", testId } } };
        var states = new State[positions];
        var owners = new int[positions];
        var counters = new int[positions];
        var acquisitions = 0;

        var workers = new Task[Environment.ProcessorCount * 2];
        for (var i = 0; i < workers.Length; i++)
            workers[i] = Task.Run(() => WorkerAsync(Random.Shared.Next()), TestToken);

        await Task.WhenAll(workers).WaitAsync(TimeSpan.FromMinutes(2), TestToken);

        Equal(acquisitions, counters.Sum());
        All(states, static s => Equal(State.Free, s));
        Equal(0L, Volatile.Read(in suspendedCallers));
        True(Volatile.Read(in parkedCallers) > 0L);

        async Task WorkerAsync(int seed)
        {
            var random = new Random(seed);
            for (var i = 0; i < iterations; i++)
            {
                var position = random.Next(positions);
                if (!await AcquireAsync(position, random))
                    continue;

                Equal(0, Interlocked.Exchange(ref owners[position], 1));
                counters[position]++;

                if (random.Next(4) is 0)
                    await Task.Yield();

                Volatile.Write(ref owners[position], 0);
                lot.Unpark(position, ref states[position]);
                Interlocked.Increment(ref acquisitions);
            }
        }

        async ValueTask<bool> AcquireAsync(int position, Random random)
        {
            switch (random.Next(3))
            {
                case 0:
                    await Park(position, TestToken);
                    return true;
                case 1:
                    return await ParkWithTimeout(position, TimeSpan.FromTicks(random.Next(1, 10_000)));
                default:
                    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(TestToken))
                    {
                        cts.CancelAfter(random.Next(0, 2));
                        try
                        {
                            await Park(position, cts.Token);
                            return true;
                        }
                        catch (OperationCanceledException) when (cts.IsCancellationRequested)
                        {
                            return false;
                        }
                    }
            }
        }

        ValueTask Park(int position, CancellationToken token)
            => lot.ParkAsync(position, ref states[position], token: token);

        ValueTask<bool> ParkWithTimeout(int position, TimeSpan timeout)
            => lot.ParkAsync(position, ref states[position], null, timeout, TestToken);
    }
}
