namespace DotNext;

public class DelegateHelpersTests : Assert
{
    [Fact]
    public static unsafe void CreateAction()
    {
        Action.FromPointer(&DoAction).Invoke();

        static void DoAction()
        {
        }
    }
}