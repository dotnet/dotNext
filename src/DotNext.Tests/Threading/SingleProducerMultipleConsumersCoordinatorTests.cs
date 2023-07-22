namespace DotNext.Threading;

public sealed class SingleProducerMultipleConsumersCoordinatorTests : Test
{
    [Fact]
    public static async Task ValveSwitch()
    {
        using var coordinator = new SingleProducerMultipleConsumersCoordinator();
        var task1 = coordinator.WaitAsync().AsTask();
        var task2 = coordinator.WaitAsync().AsTask();
        coordinator.SwitchValve();

        var task3 = coordinator.WaitAsync().AsTask();
        var task4 = coordinator.WaitAsync().AsTask();

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
}