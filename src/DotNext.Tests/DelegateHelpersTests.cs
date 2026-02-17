using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext;

using Reflection;

public sealed class DelegateHelpersTests : Test
{
    [Fact]
    public static void ChangeDelegateType()
    {
        WaitCallback callback = static _ => { };
        callback += static _ => { };
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

    [Fact]
    public static void ActionBindingChain()
    {
        var acc = new Accumulator();
        var action = new Action<string, string, string, string, string>(acc.Sum) << "abc" << "d" << "e" << "f" << "g";
        action.Invoke();
        Equal("abcdefg", acc.Value);

        acc.Value = null;
        action = new Action<string, string, string, string, string>(acc.Sum)
            .Bind("abc")
            .Bind("d")
            .Bind("e")
            .Bind("f")
            .Bind("g");
        action.Invoke();
        Equal("abcdefg", acc.Value);
        acc.Value = null;

        action
            .Unbind<string>()
            .Unbind<string, string>()
            .Unbind<string, string, string>()
            .Unbind<string, string, string, string>()
            .Unbind<string, string, string, string, string>()
            .Invoke("abc", "d", "e", "f", "g");
        Equal("abcdefg", acc.Value);
        acc.Value = null;

        var staticAction = action
            .Unbind<string>()
            .Unbind<string, string>()
            .Unbind<string, string, string>()
            .Unbind<string, string, string, string>()
            .Unbind<string, string, string, string, string>()
            .Unbind<Accumulator, string, string, string, string, string>();
        staticAction.Invoke(acc, "abc", "d", "e", "f", "g");
        Equal("abcdefg", acc.Value);
    }
    
    private sealed class Accumulator
    {
        public string Value;

        public void Sum(string x1, string x2, string x3, string x4, string x5)
            => Value += x1 + x2 + x3 + x4 + x5;
    }

    [Fact]
    public static void BindUnbindPredicate()
    {
        Predicate<string> predicate = string.IsNullOrEmpty;
        var func = predicate << string.Empty;
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
            return Single(typeof(DelegateHelpers).GetMethods(flags),
                candidate => IsInvoker(candidate, argCount));
        }

        static bool IsInvoker(MethodInfo candidate, int argCount)
        {
            if (candidate.Name is nameof(DelegateHelpers.TryInvoke))
            {
                var parameters = candidate.GetParameters();
                var delegateType = parameters[0].ParameterType;
                if (delegateType.IsDelegate && delegateType.Name == $"Func`{argCount + 1}" && parameters.Length == argCount + 1)
                    return true;
            }

            return false;
        }

        var successValue = Expression.Constant(42, typeof(int));
        var failedValue = Expression.Throw(Expression.New(typeof(ArithmeticException)), typeof(int));
        for (var argCount = 0; argCount <= 6; argCount++)
        {
            var types = new Type[argCount + 1];
            Array.Fill(types, typeof(string));
            types[argCount] = typeof(int);
            var funcType = Expression.GetFuncType(types);
            var parameters = new ParameterExpression[argCount];
            parameters.ForEach(static (p, _) => p.Value = Expression.Parameter(typeof(string)));
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
        Predicate<object> nullChecker = Predicate<object>.IsNull;
        False(nullChecker(new object()));
        nullChecker = Predicate<object>.IsNotNull;
        False(nullChecker(null));
    }

    [Fact]
    public static void ConstantProvider()
    {
        Same(Func<string>.Constant(null), Func<string>.Constant(null));
        Null(Func<string>.Constant(null).Invoke());
        
        Same(Func<int, string>.Constant(null), Func<int, string>.Constant(null));
        Null(Func<int, string>.Constant(null).Invoke(42));

        Same(Func<bool>.Constant(true), Func<bool>.Constant(true));
        Same(Func<bool>.Constant(false), Func<bool>.Constant(false));
        
        Same(Func<int, bool>.Constant(true), Func<int, bool>.Constant(true));
        Same(Func<int, bool>.Constant(false), Func<int, bool>.Constant(false));

        Equal(42, Func<int>.Constant(42).Invoke());
        Equal("Hello, world", Func<string>.Constant("Hello, world").Invoke());

        Equal(42, Func<int, int>.Constant(42).Invoke(0));
        Equal("Hello, world", Func<int, string>.Constant("Hello, world").Invoke(42));
    }
    
    [Fact]
    public static void ValueTypeConst()
    {
        const long value = 42L;
        Equal(value, Func<long>.Constant(value).Target);
        Equal(value, Func<int, long>.Constant(value).Target);
    }

    [Fact]
    public static void StringConst()
    {
        const string value = "Hello, world";
        var provider = Func<string>.Constant(value);
        Same(value, provider.Target);
    }

    [Fact]
    public static void TypeCheck()
    {
        const string obj = "Hello, world!";
        True(string.IsTypeOf(obj));
        False(int.IsTypeOf(obj));
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
        Equal(42, Func<int>.Constant(42).Invoke());
        Equal("Hello, world!", Func<string>.Constant("Hello, world!").Invoke());
    }

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
    public static unsafe void CreateAction2()
    {
        var obj = new ClassWithProperty();
        var action = Action<ClassWithProperty>.FromPointer(&DoAction);
        action(obj);
        Equal(42, obj.Prop);

        var action2 = Action<int>.FromPointer(&DoAction2, obj);
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
        var action = Action<ClassWithProperty, int>.FromPointer(&DoAction);
        action(obj, 42);
        Equal(42, obj.Prop);

        var action2 = Action<int, Missing>.FromPointer(&DoAction2, obj);
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
        var action = Action<ClassWithProperty, int, Missing>.FromPointer(&DoAction);
        action(obj, 42, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = Action<int, Missing, Missing>.FromPointer(&DoAction2, obj);
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
        var action = Action<ClassWithProperty, int, Missing, Missing>.FromPointer(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = Action<int, Missing, Missing, Missing>.FromPointer(&DoAction2, obj);
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
        var action = Action<ClassWithProperty, int, Missing, Missing, Missing>.FromPointer(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = Action<int, Missing, Missing, Missing, Missing>.FromPointer(&DoAction2, obj);
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
        var action = Action<ClassWithProperty, int, Missing, Missing, Missing, Missing>.FromPointer(&DoAction);
        action(obj, 42, Missing.Value, Missing.Value, Missing.Value, Missing.Value);
        Equal(42, obj.Prop);

        var action2 = Action<int, Missing, Missing, Missing, Missing, Missing>.FromPointer(&DoAction2, obj);
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
        var fn = Func<long>.FromPointer(&GetValue);
        Equal(42L, fn());

        var fn2 = Func<string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2());

        static long GetValue() => 42L;

        static string Constant(string value) => value;
    }

    [Fact]
    public static unsafe void CreateFunc2()
    {
        var fn = Func<long, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L));

        var fn2 = Func<Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value));

        static long GetValue(long value) => 42L;

        static string Constant(string value, Missing arg1) => value;
    }

    [Fact]
    public static unsafe void CreateFunc3()
    {
        var fn = Func<long, Missing, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L, Missing.Value));

        var fn2 = Func<Missing, Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2) => value;
    }

    [Fact]
    public static unsafe void CreateFunc4()
    {
        var fn = Func<long, Missing, Missing, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value));

        var fn2 = Func<Missing, Missing, Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3) => value;
    }

    [Fact]
    public static unsafe void CreateFunc5()
    {
        var fn = Func<long, Missing, Missing, Missing, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = Func<Missing, Missing, Missing, Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4) => value;
    }

    [Fact]
    public static unsafe void CreateFunc6()
    {
        var fn = Func<long, Missing, Missing, Missing, Missing, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = Func<Missing, Missing, Missing, Missing, Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3, Missing arg4) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5) => value;
    }

    [Fact]
    public static unsafe void CreateFunc7()
    {
        var fn = Func<long, Missing, Missing, Missing, Missing, Missing, long>.FromPointer(&GetValue);
        Equal(42L, fn(42L, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        var fn2 = Func<Missing, Missing, Missing, Missing, Missing, Missing, string>.FromPointer(&Constant, "Hello, world!");
        Equal("Hello, world!", fn2(Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value, Missing.Value));

        static long GetValue(long value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5) => 42L;

        static string Constant(string value, Missing arg1, Missing arg2, Missing arg3, Missing arg4, Missing arg5, Missing arg6) => value;
    }

    [Fact]
    public static unsafe void CreateSpanAction()
    {
        const string expected = "Hello, world!";
        var fn = SpanAction<char, string>.FromPointer(&FillChars);
        Equal(expected, string.Create(expected.Length, expected, fn));

        static void FillChars(Span<char> dest, string source)
            => source.CopyTo(dest);
    }

    [Fact]
    public static void ActionWrapper()
    {
        var i = 0;
        Equal(42, new Action<int>(SetLocalValue).Identity.Invoke(42));
        Equal(42, i);

        void SetLocalValue(int value) => i = value;
    }

    [Fact]
    public static void Identity()
    {
        Equal(42, Func<int, int>.Identity(42));
    }

    [Fact]
    public static void ToAsync1()
    {
        var func = new Action(static () => { }).ToAsync();
        True(func.Invoke(new(canceled: false)).IsCompletedSuccessfully);
        True(func.Invoke(new(canceled: true)).IsCanceled);

        func = new Action(static () => throw new Exception()).ToAsync();
        True(func.Invoke(new(canceled: false)).IsFaulted);
    }

    [Fact]
    public static void ToAsync2()
    {
        var func = new Action<int>(static _ => { }).ToAsync();
        True(func.Invoke(42, new(canceled: false)).IsCompletedSuccessfully);
        True(func.Invoke(42, new(canceled: true)).IsCanceled);

        func = new Action<int>(static _ => throw new Exception()).ToAsync();
        True(func.Invoke(42, new(canceled: false)).IsFaulted);
    }
    
    [Fact]
    public static void ToAsync3()
    {
        var func = new Action<int, int>(static (_, _) => { }).ToAsync();
        True(func.Invoke(42, 42, new(canceled: false)).IsCompletedSuccessfully);
        True(func.Invoke(42, 42, new(canceled: true)).IsCanceled);

        func = new Action<int, int>(static (_, _) => throw new Exception()).ToAsync();
        True(func.Invoke(42, 42, new(canceled: false)).IsFaulted);
    }

    [Fact]
    public static async Task ToAsync4()
    {
        var func = new Func<int, int, int>(static (x, y) => x + y).ToAsync();
        Equal(84, await func.Invoke(42, 42, new(canceled: false)));
        True(func.Invoke(42, 42, new(canceled: true)).IsCanceled);

        func = new Func<int, int, int>(static (_, _) => throw new Exception()).ToAsync();
        await ThrowsAsync<Exception>(func.Invoke(42, 42, new(canceled: false)).AsTask);
    }
    
    [Fact]
    public static async Task ToAsync5()
    {
        var func = new Func<int, int>(Func<int, int>.Identity).ToAsync();
        Equal(42, await func.Invoke(42, new(canceled: false)));
        True(func.Invoke(42, new(canceled: true)).IsCanceled);

        func = new Func<int, int>(static _ => throw new Exception()).ToAsync();
        await ThrowsAsync<Exception>(func.Invoke(42, new(canceled: false)).AsTask);
    }
    
    [Fact]
    public static async Task ToAsync6()
    {
        var func = Func<int>.Constant(42).ToAsync();
        Equal(42, await func.Invoke(new(canceled: false)));
        True(func.Invoke(new(canceled: true)).IsCanceled);

        func = new Func<int>(static () => throw new Exception()).ToAsync();
        await ThrowsAsync<Exception>(func.Invoke(new(canceled: false)).AsTask);
    }

    [Fact]
    public static void HideReturnValue1()
    {
        var box = new StrongBox<int>();
        var action = new Func<int>(ChangeValue).HideReturnValue();
        action.Invoke();
        Equal(42, box.Value);
        
        int ChangeValue() => box.Value = 42;
    }
    
    [Fact]
    public static void HideReturnValue2()
    {
        var box = new StrongBox<int>();
        var action = new Func<int, int>(ChangeValue).HideReturnValue();
        action.Invoke(42);
        Equal(42, box.Value);
        
        int ChangeValue(int value) => box.Value = value;
    }
    
    [Fact]
    public static void TryInvokeAction()
    {
        static MethodInfo GetMethod(int argCount)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
            return Single(typeof(DelegateHelpers).GetMethods(flags),
                candidate => IsInvoker(candidate, argCount));
        }

        static bool IsInvoker(MethodInfo candidate, int argCount)
        {
            if (candidate.Name is nameof(DelegateHelpers.TryInvoke))
            {
                var parameters = candidate.GetParameters();
                var delegateType = parameters[0].ParameterType;
                if (delegateType.IsDelegate)
                {
                    var condition = argCount switch
                    {
                        0 => delegateType.Name is nameof(Action),
                        _ => delegateType.Name == $"Action`{argCount}"
                    };

                    if (condition && parameters.Length == argCount + 1)
                        return true;
                }
            }

            return false;
        }

        var successValue = Expression.Empty();
        var failedValue = Expression.Throw(Expression.New(typeof(ArithmeticException)), typeof(void));
        for (var argCount = 0; argCount <= 6; argCount++)
        {
            var types = new Type[argCount];
            Array.Fill(types, typeof(string));
            var actionType = Expression.GetActionType(types);
            var parameters = new ParameterExpression[argCount];
            parameters.ForEach(static (p, _) => p.Value = Expression.Parameter(typeof(string)));
            //prepare args
            var args = new object[parameters.LongLength + 1];
            Array.Fill(args, string.Empty);
            //find method to test
            var method = types is [] ? GetMethod(argCount) : GetMethod(argCount).MakeGenericMethod(types);
            //check success scenario
            args[0] = Expression.Lambda(actionType, successValue, parameters).Compile();
            var result = (Exception)method.Invoke(null, args);
            Null(result);
            //check failure
            args[0] = Expression.Lambda(actionType, failedValue, parameters).Compile();
            result = (Exception)method.Invoke(null, args);
            IsType<ArithmeticException>(result);
        }
    }
    
    [Fact]
    public static void OrAndXor()
    {
        Func<int, bool> pred1 = static i => i > 10;
        Func<int, bool> pred2 = static i => i < 0;
        True((pred1 | pred2).Invoke(11));
        True((pred1 | pred2).Invoke(-1));
        False((pred1 | pred2).Invoke(8));

        pred2 = static i => i > 20;
        True((pred1 & pred2).Invoke(21));
        False((pred1 & pred2).Invoke(19));
        False((pred1 ^ pred2).Invoke(21));
        False((pred1 ^ pred2).Invoke(1));
        True((pred1 ^ pred2).Invoke(19));
    }

    [Fact]
    public static void NoOperation()
    {
        Same(Action.NoOp, Action.NoOp);
        Action.NoOp.Invoke();
        Action<int>.NoOp.Invoke(42);
        Action<int, int>.NoOp.Invoke(42, 42);
        Action<int, int, int>.NoOp.Invoke(42, 42, 42);
        Action<int, int, int, int>.NoOp.Invoke(42, 42, 42, 42);
        Action<int, int, int, int, int>.NoOp.Invoke(42, 42, 42, 42, 42);
        Action<int, int, int, int, int, int>.NoOp.Invoke(42, 42, 42, 42, 42, 42);
    }
}