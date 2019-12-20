using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using Xunit;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    public sealed class DelegateHelpersTests : Assert
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
            WaitCallback callback = obj => { };
            callback += obj => { };
            var result = callback.ChangeType<SendOrPostCallback>();
            NotNull(result);
            var list1 = callback.GetInvocationList().Select(d => d.Method);
            var list2 = result.GetInvocationList().Select(d => d.Method);
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
            var d = DelegateHelpers.CreateOpenDelegate<Func<string, int>>(str => str.Length);
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
            var d = DelegateHelpers.CreateOpenDelegate<Func<decimal, long>>(i => (long)i);
            Equal(42L, d(42M));
        }

        private static int GetLength(string value) => value.Length;

        [Fact]
        public static void BindUnbind()
        {
            var func = new Func<string, int>(GetLength).Bind("abc");
            Equal(3, func());
            Equal(4, func.Unbind<string, int>().Invoke("abcd"));
        }

        [Fact]
        public static void TryInvokeFunc()
        {
            static MethodInfo GetMethod(int argCount)
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
                foreach (var candidate in typeof(Func).GetMethods(flags))
                    if (candidate.Name == nameof(Func.TryInvoke) && candidate.GetParameters().Length == argCount + 1)
                        return candidate;
                throw new Xunit.Sdk.XunitException();
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
                parameters.ForEach((ref ParameterExpression p, long idx) => p = Expression.Parameter(typeof(string)));
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
        public static void Conversion()
        {
            var conv = new Converter<string, int>(int.Parse);
            Equal(42, conv.AsFunc().Invoke("42"));
            Converter<int, bool> odd = i => i % 2 != 0;
            True(odd.AsPredicate().Invoke(3));
            Equal(42, conv.TryInvoke("42"));
            NotNull(conv.TryInvoke("abc").Error);
        }
    }
}