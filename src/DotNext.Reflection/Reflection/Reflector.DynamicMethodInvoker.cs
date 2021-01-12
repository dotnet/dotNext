using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    using static Collections.Generic.Sequence;
    using static Runtime.CompilerServices.ReflectionUtils;

    public static partial class Reflector
    {
        private static unsafe DynamicInvoker Unreflect<TMethod>(TMethod method, delegate*<Expression?, TMethod, IEnumerable<Expression>, Expression> resultBuilder)
            where TMethod : MethodBase
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(Span<object?>));
            Expression? thisArg;
            if (method.IsStatic || method.MemberType == MemberTypes.Constructor || method.DeclaringType is null)
                thisArg = null;
            else if (method.DeclaringType.IsValueType)
                thisArg = Expression.Unbox(target, method.DeclaringType);
            else
                thisArg = Expression.Convert(target, method.DeclaringType);

            ICollection<Expression> arglist = new LinkedList<Expression>(), prologue = new LinkedList<Expression>(), epilogue = new LinkedList<Expression>();
            ICollection<ParameterExpression> tempVars = new LinkedList<ParameterExpression>();

            // handle parameters
            foreach (var parameter in method.GetParameters())
            {
                var position = Expression.Constant(parameter.Position);
                var getter = Get(arguments, position);
                Func<Expression, MethodCallExpression> setter = value => Set(arguments, position, value);
                Expression argument;

                if (parameter.ParameterType.IsByRefLike)
                {
                    throw new NotSupportedException();
                }
                else if (parameter.ParameterType.IsByRef)
                {
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
                            epilogue.Add(setter(Wrap(tempVar)));
                        else
                            epilogue.Add(setter(tempVar));
                        argument = tempVar;
                    }
                }
                else if (parameter.ParameterType.IsPointer)
                {
                    argument = Unwrap(getter, parameter.ParameterType);
                }
                else
                {
                    argument = Expression.Convert(getter, parameter.ParameterType);
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
            if (epilogue.Count == 0)
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
}