using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AsyncBarrierTests : Test
{
    [Fact]
    public static async Task RemovingWaitingParticipants()
    {
        using var barrier = new AsyncBarrier(4);
        var task = barrier.SignalAndWaitAsync();
        Equal(3, barrier.ParticipantsRemaining);
        barrier.RemoveParticipants(2);
        Equal(1, barrier.ParticipantsRemaining);
        Throws<ArgumentOutOfRangeException>(() => barrier.RemoveParticipants(20));
        Equal(1, barrier.ParticipantsRemaining);
        barrier.RemoveParticipant();
        Equal(0, barrier.ParticipantsRemaining);
        await task;
    }

    [Fact]
    public static async Task AddRemoveParticipant()
    {
        for (var j = 0; j < 100; j++)
        {
            using var barrier = new AsyncBarrier(0);
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
            Equal(0, barrier.ParticipantCount);
        }
    }

    [Fact]
    public static async Task PhaseCompletion()
    {
        using var barrier = new AsyncBarrier(3);
        ICollection<Task> tasks = new LinkedList<Task>();
        Equal(0, barrier.CurrentPhaseNumber);
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        await Task.WhenAll(tasks);
        Equal(1, barrier.CurrentPhaseNumber);

        tasks.Clear();
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        tasks.Add(barrier.SignalAndWaitAsync().AsTask());
        await Task.WhenAll(tasks);
        Equal(2, barrier.CurrentPhaseNumber);
    }

    [Fact]
    public static async Task RegressionIssue73()
    {
        using var barrier = new AsyncBarrier(2);

        var task1 = Task.Run(async () =>
        {
            await barrier.SignalAndWaitAsync();
            return 24;
        });

        var task2 = Task.Run(async () =>
        {
            await barrier.SignalAndWaitAsync();
            return 42;
        });

        var result = await Task.WhenAll(task1, task2);
        Equal(24, result[0]);
        Equal(42, result[1]);
    }
}