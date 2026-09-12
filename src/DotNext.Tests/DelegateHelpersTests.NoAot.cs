using System.Linq.Expressions;
using System.Reflection;

namespace DotNext;

using Reflection;

partial class DelegateHelpersTests
{
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
}