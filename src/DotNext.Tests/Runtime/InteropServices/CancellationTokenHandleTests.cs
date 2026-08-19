namespace DotNext.Runtime.InteropServices;

public class CancellationTokenHandleTests : Test
{
    [Fact]
    public static void DefaultHandle()
    {
        var handle = default(CancellationTokenHandle);
        Equal(CancellationToken.None, handle.Token);
        
        handle = new(CancellationToken.None);
        Equal(CancellationToken.None, handle.Token);
    }

    [Fact]
    public static void AllocatedHandle()
    {
        using var handle = new CancellationTokenHandle(new CancellationToken(canceled: true));
        Equal(new CancellationToken(canceled: true), handle.Token);
    }
}