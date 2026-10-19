namespace DotNext.Hosting;

public sealed class ApplicationLockTests : Test
{
    [Fact]
    public static void CannotBeAcquiredMoreThanOnce()
    {
        True(ApplicationLock.IsSupported);
        
        using var lock1 = new ApplicationLock();
        True(lock1.TryAcquire());

        using (var lock2 = new ApplicationLock())
        {
            False(lock2.TryAcquire());
            Throws<SynchronizationLockException>(lock2.Release);
        }
        
        lock1.Release();
    }
}