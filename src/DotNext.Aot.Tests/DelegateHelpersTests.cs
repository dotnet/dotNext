namespace DotNext;

public class DelegateHelpersTests : Assert
{
    [Fact]
    public static unsafe void CreateAction()
    {
        var action = Action.FromPointer(&DoAction);
        NotNull(action);
        action();

        var obj = new ClassWithProperty();
        action = Action.FromPointer(&DoAction2, obj);
        action();
        Equal(42, obj.Prop);

        static void DoAction()
        {
        }

        static void DoAction2(ClassWithProperty obj)
            => obj.Prop = 42;
    }
    
    [Fact]
    public static void FuncBindingChain()
    {
        var func = new Func<string, string, string, string, string, string>(Concat) << "abc" << "d" << "e" << "f" << "g";
        Equal("abcdefg", func());

        func = new Func<string, string, string, string, string, string>(Concat)
            .Bind("abc")
            .Bind("d")
            .Bind("e")
            .Bind("f")
            .Bind("g");
        Equal("abcdefg", func());
        
        Equal("abcdefg", func
            .Unbind<string, string>()
            .Unbind<string, string, string>()
            .Unbind<string, string, string, string>()
            .Unbind<string, string, string, string, string>()
            .Unbind<string, string, string, string, string, string>()
            .Invoke("abc", "d", "e", "f", "g"));
        
        static string Concat(string str1, string str2, string str3, string str4, string str5)
            => str1 + str2 + str3 + str4 + str5;
    }
}

file sealed class ClassWithProperty
{
    internal int Prop { get; set; }
}