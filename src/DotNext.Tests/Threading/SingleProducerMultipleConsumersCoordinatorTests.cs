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
}