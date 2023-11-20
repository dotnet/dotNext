using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace DotNext;

public sealed class DelegateHelpersTests : Test
{
    [Fact]
    public static void ContravarianceTest()
    {
        EventHandler<string> handler = null;
        EventHandler<object> dummy = (sender, args) => { };
        handler += dummy.Contravariant<object, string>();
        NotNull(handler);
        handler -= dummy.Contravariant<object, string>();
        Null(handler);
    }

    [Fact]
    public static void ChangeDelegateType()
    {
        WaitCallback callback = static obj => { };
        callback += static obj => { };
        var result = callback.ChangeType<SendOrPostCallback>();
        NotNull(result);
        var list1 = callback.GetInvocationList().Select(static d => d.Method);
        var list2 = result.GetInvocationList().Select(static d => d.Method);
        Equal(list1, list2);
    }

    [Fact]
    public static void OpenDelegate()
    {
        var d = DelegateHelpers.CreateOpenDelegate<Func<string, char, int, int>>((str, ch, startIndex) => str.IndexOf(ch, startIndex));
        NotNull(d);
        Equal(1, d("abc", 'b', 0));
    }

    [Fact]
    public static void OpenDelegateForProperty()
    {
        var d = DelegateHelpers.CreateOpenDelegate<Func<string, int>>(static str => str.Length);
        NotNull(d);
        Equal(4, d("abcd"));
    }

    [Fact]
    public static void ClosedDelegate()
    {
        var d = DelegateHelpers.CreateClosedDelegateFactory<Func<char, int, int>>((ch, startIndex) => "".IndexOf(ch, startIndex)).Invoke("abc");
        Equal(1, d('b', 0));
    }

    [Fact]
    public static void ClosedDelegateForProperty()
    {
        var d = DelegateHelpers.CreateClosedDelegateFactory<Func<int>>(() => "".Length).Invoke("abcd");
        Equal(4, d());
    }

    [Fact]
    public static void OpenDelegateConversion()
    {
        var d = DelegateHelpers.CreateOpenDelegate<Func<decimal, long>>(static i => (long)i);
        Equal(42L, d(42M));
    }

    private static int GetLength(string value) => value.Length;

    [Fact]
    public static void BindUnbind1()
    {
        var func = new Func<string, int>(GetLength).Bind("abc");
        Equal(3, func());
        Equal(4, func.Unbind<string, int>().Invoke("abcd"));

        var func2 = new Func<string, bool>("abc".Contains).Bind("a");
        True(func2());
        True(func2.Unbind<string, bool>().Invoke("c"));
        Throws<InvalidOperationException>(() => func2.Unbind<object, bool>());
    }

    [Fact]
    public static void BindUnbind2()
    {
        var func = new Func<string, string, string>(string.Concat).Bind("abc");
        Equal("abcde", func("de"));
        Equal("abcde", func.Unbind<string, string, string>().Invoke("ab", "cde"));

        var func2 = new Func<string, string, string>("abc".Replace).Bind("a");
        Equal("1bc", func2("1"));
        Equal("2bc", func2.Unbind<string, string, string>().Invoke("a", "2"));
        Throws<InvalidOperationException>(() => func2.Unbind<object, string, string>());
    }

    [Fact]
    public static void BindUnbind3()
    {
        var func = new Func<string, string, string, string>(string.Concat).Bind("abc");
        Equal("abcde", func("d", "e"));
        Equal("abcde", func.Unbind<string, string, string, string>().Invoke("ab", "cd", "e"));

        var func2 = new Func<string, string, StringComparison, string>("abc".Replace).Bind("a");
        Equal("1bc", func2("1", StringComparison.Ordinal));
        Equal("2bc", func2.Unbind<string, string, StringComparison, string>().Invoke("a", "2", StringComparison.Ordinal));
    }

    [Fact]
    public static void BindUnbind4()
    {
        var func = new Func<string, string, string, string, string>(string.Concat).Bind("abc");
        Equal("abcdef", func("d", "e", "f"));
        Equal("abcdef", func.Unbind<string, string, string, string, string>().Invoke("ab", "cd", "e", "f"));

        var func2 = new Func<string, string, bool, CultureInfo, string>("abc".Replace).Bind("a");
        Equal("1bc", func2("1", false, CultureInfo.InvariantCulture));
        Equal("2bc", func2.Unbind<string, string, bool, CultureInfo, string>().Invoke("a", "2", false, CultureInfo.InvariantCulture));
    }

    [Fact]
    public static void BindUnbind5()
    {
        static string Concat(string str1, string str2, string str3, string str4, string str5)
            => str1 + str2 + str3 + str4 + str5;
        var func = new Func<string, string, string, string, string, string>(Concat).Bind("abc");
        Equal("abcdefg", func("d", "e", "f", "g"));
        Equal("abcdefg", func.Unbind<string, string, string, string, string, string>().Invoke("ab", "cd", "e", "f", "g"));
    }

    [Fact]
    public static void BindUnbindPredicate()
    {
        Predicate<string> predicate = string.IsNullOrEmpty;
        var func = predicate.Bind(string.Empty);
        True(func());
        func = predicate.Bind("abc");
        False(func());

        predicate = func.Unbind<string>();
        True(predicate(string.Empty));
        False(predicate("abc"));
    }

    [Fact]
    public static void TryInvokeFunc()
    {
        static MethodInfo GetMethod(int argCount)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            return Single(typeof(Func).GetMethods(flags), candidate => candidate.Name == nameof(Func.TryInvoke) && candidate.GetParameters().Length == argCount + 1);
        }

        var successValue = Expression.Constant(42, typeof(int));
        var failedValue = Expression.Throw(Expression.New(typeof(ArithmeticException)), typeof(int));
        for (var argCount = 0; argCount <= 10; argCount++)
        {
            var types = new Type[argCount + 1];
            Array.Fill(types, typeof(string));
            types[argCount] = typeof(int);
            var funcType = Expression.GetFuncType(types);
            var parameters = new ParameterExpression[argCount];
            parameters.ForEach(static (ref ParameterExpression p, nint _) => p = Expression.Parameter(typeof(string)));
            //prepare args
            var args = new object[parameters.LongLength + 1];
            Array.Fill(args, string.Empty);
            //find method to test
            var method = GetMethod(argCount).MakeGenericMethod(types);
            //check success scenario
            args[0] = Expression.Lambda(funcType, successValue, parameters).Compile();
            var result = (Result<int>)method.Invoke(null, args);
            Equal(42, result);
            //check failure
            args[0] = Expression.Lambda(funcType, failedValue, parameters).Compile();
            result = (Result<int>)method.Invoke(null, args);
            IsType<ArithmeticException>(result.Error);
        }
    }

    [Fact]
    public static void FuncNullNotNull()
    {
        var nullChecker = Func.IsNull<object>().AsPredicate();
        False(nullChecker(new object()));
        nullChecker = Func.IsNotNull<object>().AsPredicate();
        False(nullChecker(null));
    }

    [Fact]
    public static void ConstantProvider()
    {
        Same(Func.Constant<string>(null), Func.Constant<string>(null));
        Null(Func.Constant<string>(null).Invoke());

        Same(Func.Constant(true), Func.Constant(true));
        Same(Func.Constant(false), Func.Constant(false));

        Equal(42, Func.Constant<int>(42).Invoke());
        Equal("Hello, world", Func.Constant<string>("Hello, world").Invoke());
    }

    [Fact]
    public static void TypeCheck()
    {
        var obj = "Hello, world!";
        True(Func.IsTypeOf<string>().Invoke(obj));
        False(Func.IsTypeOf<int>().Invoke(obj));
    }

    [Fact]
    public static void Conversion()
    {
        var conv = new Converter<string, int>(int.Parse);
        Equal(42, conv.AsFunc().Invoke("42"));
        Converter<int, bool> odd = static i => i % 2 != 0;
        True(odd.AsPredicate().Invoke(3));
        Equal(42, conv.TryInvoke("42"));
        NotNull(conv.TryInvoke("abc").Error);
    }

    private sealed class ClassWithProperty
    {
        internal int Prop { get; set; }
    }

    [Fact]
    public static void OpenDelegateForPropertySetter()
    {
        var obj = new ClassWithProperty();
        var action = DelegateHelpers.CreateOpenDelegate<ClassWithProperty, int>(static obj => obj.Prop);
        NotNull(action);
        action(obj, 42);
        Equal(42, obj.Prop);
    }

    [Fact]
    public static void Constant()
    {
        Equal(42, Func.Constant(42).Invoke());
        Equal("Hello, world!", Func.Constant("Hello, world!").Invoke());
    }

    [Fact]
    public static unsafe void CreateAction()
    {
        Action action = DelegateHelpers.CreateDelegate(&DoAction);
        NotNull(action);
        action();

        var obj = new ClassWithProperty();
        action = DelegateHelpers.CreateDelegate(&DoAction2, obj);
        action();
        Equal(42, obj.Prop);

        static void DoAction()
        {
        }

        static void DoAction2(ClassWithProperty obj)
            => obj.Prop = 42;
    }

    [Fact]
    public static unsafe void CreateAction2()
    {
        var obj = new ClassWithProperty();
        Action<ClassWithProperty> action = DelegateHelpers.CreateDelegate<ClassWithProperty>(&DoAction);
        action(obj);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int>(&DoAction2, obj);
        action2(56);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj)
            => obj.Prop = 42;

        static void DoAction2(ClassWithProperty obj, int value)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateAction3()
    {
        var obj = new ClassWithProperty();
        Action<ClassWithProperty, int> action = DelegateHelpers.CreateDelegate<ClassWithProperty, int>(&DoAction);
        action(obj, 42);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing>(&DoAction2, obj);
        action2(56, Missing.Value);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj, int value)
            => obj.Prop = value;

        static void DoAction2(ClassWithProperty obj, int value, Missing missing)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateAction4()
    {
        var obj = new ClassWithProperty();
        var action = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing>(&DoAction);
        action(obj, 42, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing>(&DoAction2, obj);
        action2(56, Missing.Value, Missing.Value);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj, int value, Missing missing)
            => obj.Prop = value;

        static void DoAction2(ClassWithProperty obj, int value, Missing arg2, Missing arg3)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateAction5()
    {
        var obj = new ClassWithProperty();
        var action = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing>(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing, Missing>(&DoAction2, obj);
        action2(56, Missing.Value, Missing.Value, Missing.Value);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj, int value, Missing arg2, Missing arg3)
            => obj.Prop = value;

        static void DoAction2(ClassWithProperty obj, int value, Missing arg2, Missing arg3, Missing arg4)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateAction6()
    {
        var obj = new ClassWithProperty();
        var action = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing, Missing>(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing, Missing, Missing>(&DoAction2, obj);
        action2(56, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj, int value, Missing arg2, Missing arg3, Missing arg4)
            => obj.Prop = value;

        static void DoAction2(ClassWithProperty obj, int value, Missing arg2, Missing arg3, Missing arg4, Missing arg5)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateAction7()
    {
        var obj = new ClassWithProperty();
        var action = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing, Missing, Missing>(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = DelegateHelpers.CreateDelegate<ClassWithProperty, int, Missing, Missing, Missing, Missing, Missing>(&DoAction2, obj);
        action2(56, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
        Equal(56, obj.Prop);

        static void DoAction(ClassWithProperty obj, int value, Missing arg2, Missing arg3, Missing arg4, Missing arg5)
            => obj.Prop = value;

        static void DoAction2(ClassWithProperty obj, int value, Missing arg2, Missing arg3, Missing arg4, Missing arg5, Missing arg6)
            => obj.Prop = value;
    }

    [Fact]
    public static unsafe void CreateFunc()
    {
        var fn = DelegateHelpers.CreateDelegate<long>(&GetValue);
        Equal(42L, fn());

        var fn2 = DelegateHelpers.CreateDelegate<string, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2());

        static long GetValue() => 42L;

        static string Constant(string value) => value;
    }

    [Fact]
    public static unsafe void CreateFunc2()
    {
        var fn = DelegateHelpers.CreateDelegate<long, long>(&GetValue);
        Equal(42L, fn(42L));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value));

        static long GetValue(long value) => 42L;

        static string Constant(string value, Missing arg1) => value;
    }

    [Fact]
    public static unsafe void CreateFunc3()
    {
        var fn = DelegateHelpers.CreateDelegate<long, Missing, long>(&GetValue);
        Equal(42L, fn(42L, Missing.Value));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2) => value;
    }

    [Fact]
    public static unsafe void CreateFunc4()
    {
        var fn = DelegateHelpers.CreateDelegate<long, Missing, Missing, long>(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, Missing, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3) => value;
    }

    [Fact]
    public static unsafe void CreateFunc5()
    {
        var fn = DelegateHelpers.CreateDelegate<long, Missing, Missing, Missing, long>(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, Missing, Missing, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4) => value;
    }

    [Fact]
    public static unsafe void CreateFunc6()
    {
        var fn = DelegateHelpers.CreateDelegate<long, Missing, Missing, Missing, Missing, long>(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, Missing, Missing, Missing, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3, Missing arg4) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5) => value;
    }

    [Fact]
    public static unsafe void CreateFunc7()
    {
        var fn = DelegateHelpers.CreateDelegate<long, Missing, Missing, Missing, Missing, Missing, long>(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = DelegateHelpers.CreateDelegate<string, Missing, Missing, Missing, Missing, Missing, Missing, string>(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5, Missing arg6) => value;
    }

    [Fact]
    public static unsafe void CreateSpanAction()
    {
        const string expected = "Hello, world!";
        var fn = DelegateHelpers.CreateDelegate<char, string>(&FillChars);
        Equal(expected, string.Create(expected.Length, expected, fn));

        static void FillChars(Span<char> dest, string source)
            => source.CopyTo(dest);
    }

    [Fact]
    public static void ActionWrapper()
    {
        var i = 0;
        Equal(42, new Action<int>(SetLocalValue).Identity<int>().Invoke(42));
        Equal(42, i);

        void SetLocalValue(int value) => i = value;
    }
}