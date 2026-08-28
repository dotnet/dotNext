namespace DotNext;

public class DelegateHelpersTests : Assert
{
    [Fact]
    public static unsafe void CreateAction()
    {
        Throws<PlatformNotSupportedException>(static () => Action.FromPointer(&DoAction));

        static void DoAction()
        {
        }
    }
}