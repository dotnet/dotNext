using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Threading
{
    public sealed class AsyncBarrierTests: Assert
    {
        [Fact]
        public static async Task RemovingWaitingParticipants()
        {
            using(var barrier = new AsyncBarrier(4))
            {
                var task = barrier.SignalAndWait();
                while (barrier.ParticipantsRemaining > 3)
                    await Task.Delay(100);
                barrier.RemoveParticipants(2);
                Equal(1, barrier.ParticipantsRemaining);
                Throws<ArgumentOutOfRangeException>(() => barrier.RemoveParticipants(20));
                Equal(1, barrier.ParticipantsRemaining);
                barrier.RemoveParticipant();
                Equal(0, barrier.ParticipantsRemaining);
            }
        }

        [Fact]
        public static async Task AddRemoveParticipant()
        {
            for (var j = 0; j < 100; j++)
            {
                using(var barrier = new AsyncBarrier(0))
                {
                    var actions = new Action[4];
                    for (int k = 0; k < 4; k++)
                    {
                        actions[k] = () =>
                        {
                            for (int i = 0; i < 400; i++)
                            {
                                barrier.AddParticipant();
                                barrier.RemoveParticipant();
                            }
                        };
                    }

                    var tasks = new Task[actions.Length];
                    for (var k = 0; k < tasks.Length; k++)
                        tasks[k] = Task.Factory.StartNew(index => actions[Convert.ToInt32(index)](), k, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                    await Task.WhenAll(tasks);
                    Assert.Equal(0, barrier.ParticipantCount);
                }
            }
        }

        [Fact]
        public static async Task CorrectGarbageCollection()
        {
            for (int j = 0; j < 10; j++)
            {
                Task t1, t2;
                using(var barrier = new AsyncBarrier(3))
                {
                    t1 = barrier.SignalAndWait();
                    t2 = barrier.SignalAndWait();

                    await barrier.SignalAndWait();

                    GC.Collect();

                    await Task.WhenAll(t1, t2);
                }
            }
        }

        [Fact]
        public static async Task PhaseCompletion()
        {
            using(var barrier = new AsyncBarrier(3))
            {
                Equal(0, barrier.CurrentPhaseNumber);
                var phaseTask = barrier.Wait();
                foreach(var index in Enumerable.Range(0, 3))
                    ThreadPool.QueueUserWorkItem(state => barrier.SignalAndWait().Wait());
                await phaseTask;
                Equal(1, barrier.CurrentPhaseNumber);
                phaseTask = barrier.Wait();
                foreach(var index in Enumerable.Range(0, 3))
                    ThreadPool.QueueUserWorkItem(state => barrier.SignalAndWait());
                await phaseTask;
                Equal(2, barrier.CurrentPhaseNumber);
            }
        }
    }
}