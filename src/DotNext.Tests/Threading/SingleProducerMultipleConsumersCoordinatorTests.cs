namespace DotNext.Threading;

[Collection(TestCollections.AsyncPrimitives)]
public sealed class SingleProducerMultipleConsumersCoordinatorTests : Test
{
    [Fact]
    public static async Task ValveSwitch()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        var task1 = coordinator.WaitAsync(TestToken).AsTask();
        var task2 = coordinator.WaitAsync(TestToken).AsTask();
        coordinator.SwitchValve();

        var task3 = coordinator.WaitAsync(TestToken).AsTask();
        var task4 = coordinator.WaitAsync(TestToken).AsTask();

        coordinator.Drain();
        await task1;
        await task2;
        False(task3.IsCompleted);
        False(task4.IsCompleted);

        coordinator.SwitchValve();
        coordinator.Drain();
        await task3;
        await task4;
    }

    [Fact]
    public static async Task StampCapturedWhileIdle()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        var stamp = coordinator.Stamp;

        // the round started after the capture confirms the stamp
        coordinator.SwitchValve();
        var task = coordinator.WaitAsync(stamp, TestToken).AsTask();
        False(task.IsCompleted);

        coordinator.Drain();
        await task;
        True(coordinator.WaitAsync(stamp, TestToken).IsCompletedSuccessfully);
    }

    [Fact]
    public static async Task StampCapturedWhileRoundInProgress()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        coordinator.SwitchValve();
        var stamp = coordinator.Stamp;
        var task = coordinator.WaitAsync(stamp, TestToken).AsTask();

        // the round started before the capture doesn't confirm the stamp
        coordinator.Drain();
        False(task.IsCompleted);
        False(coordinator.WaitAsync(stamp, TestToken).IsCompleted);

        coordinator.SwitchValve();
        coordinator.Drain();
        await task;
    }

    [Fact]
    public static void IncompleteRound()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        var stamp = coordinator.Stamp;

        // the round without Drain() never confirms the stamp
        coordinator.SwitchValve();
        False(coordinator.WaitAsync(stamp, TestToken).IsCompleted);
    }

    [Fact]
    public static async Task UnfairDrain()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        var ticket = coordinator.Stamp; // idle: needs the end of the next round
        coordinator.SwitchValve();
        var late = coordinator.WaitAsync(TestToken).AsTask(); // mid-round: needs the end of the round after
        var early = coordinator.WaitAsync(ticket, TestToken).AsTask(); // queued after 'late'

        coordinator.Drain();
        await early; // with fair draining it would be stuck behind 'late'
        False(late.IsCompleted);
    }
}