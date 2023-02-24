using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
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

        coordinator.Signal();
        await task1;
        await task2;
        False(task3.IsCompleted);
        False(task4.IsCompleted);

        coordinator.SwitchValve();
        coordinator.Signal();
        await task3;
        await task4;
    }
}