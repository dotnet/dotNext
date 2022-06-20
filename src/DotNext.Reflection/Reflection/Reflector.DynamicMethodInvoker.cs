using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection;

using static Runtime.CompilerServices.ReflectionUtils;

public static partial class Reflector
{
    [SuppressMessage("StyleCop.CSharp.SpacingRules", "SA1013", Justification = "False positive")]
    private static unsafe DynamicInvoker Unreflect<TMethod>(TMethod method, delegate*<Expression?, TMethod, IEnumerable<Expression>, Expression> resultBuilder)
        where TMethod : MethodBase
    {
        var target = Expression.Parameter(typeof(object));
        var arguments = Expression.Parameter(typeof(Span<object?>));
        Expression? thisArg = method switch
        {
            { IsStatic: true } or { MemberType: MemberTypes.Constructor } or { DeclaringType: null } => null,
            { DeclaringType: { IsValueType: true } } => Expression.Unbox(target, method.DeclaringType),
            _ => Expression.Convert(target, method.DeclaringType)
        };

        ICollection<Expression> arglist = new LinkedList<Expression>(), prologue = new LinkedList<Expression>(), epilogue = new LinkedList<Expression>();
        ICollection<ParameterExpression> tempVars = new LinkedList<ParameterExpression>();

        // handle parameters
        foreach (var parameter in method.GetParameters())
        {
            var position = Expression.Constant(parameter.Position);
            var getter = Get(arguments, position);
            MethodCallExpression SetArgument(Expression value) => Set(arguments, position, value);
            Expression argument;

            switch (parameter.ParameterType)
            {
                case { IsByRefLike: true }:
                    throw new NotSupportedException();
                case { IsByRef: true }:
                    var parameterType = parameter.ParameterType.GetElementType();
                    Debug.Assert(parameterType is not null);

                    // value type parameter can be passed as unboxed reference
                    if (parameterType.IsValueType)
                    {
                        argument = Expression.Unbox(getter, parameterType);
                    }
                    else
                    {
                        var tempVar = Expression.Variable(parameterType);
                        tempVars.Add(tempVar);
                        prologue.Add(Expression.Assign(tempVar, parameterType.IsPointer ? Unwrap(getter, parameterType) : Expression.Convert(getter, parameterType)));
                        if (parameterType.IsPointer)
                            epilogue.Add(SetArgument(Wrap(tempVar)));
                        else
                            epilogue.Add(SetArgument(tempVar));
                        argument = tempVar;
                    }

                    break;
                case { IsPointer: true }:
                    argument = Unwrap(getter, parameter.ParameterType);
                    break;
                default:
                    argument = Expression.Convert(getter, parameter.ParameterType);
                    break;
            }

            arglist.Add(argument);
        }

        // construct body of the method
        Expression result = resultBuilder(thisArg, method, arglist);
        if (result.Type.IsByRefLike)
            throw new NotSupportedException();
        else if (result.Type == typeof(void))
            epilogue.Add(Expression.Default(typeof(object)));
        else if (result.Type.IsPointer)
            result = Wrap(result);
        else if (result.Type.IsValueType)
            result = Expression.Convert(result, typeof(object));

        // construct lambda expression
        bool useTailCall;
        if (epilogue.Count is 0)
        {
            useTailCall = true;
        }
        else if (result.Type == typeof(void))
        {
            result = Expression.Block(typeof(object), tempVars, prologue.Append(result).Concat(epilogue));
            useTailCall = false;
        }
        else
        {
            var resultVar = Expression.Variable(typeof(object));
            tempVars.Add(resultVar);
            result = Expression.Assign(resultVar, result);
            epilogue.Add(resultVar);
            result = Expression.Block(typeof(object), tempVars, prologue.Append(result).Concat(epilogue));
            useTailCall = false;
        }

        // help GC
        arglist.Clear();
        prologue.Clear();
        epilogue.Clear();
        tempVars.Clear();
        return Expression.Lambda<DynamicInvoker>(result, useTailCall, target, arguments).Compile();
    }
}