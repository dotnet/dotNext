using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    using static Collections.Generic.Sequence;
    using static Runtime.CompilerServices.ReflectionUtils;

    public static partial class Reflector
    {
        // TODO: Replace resultBuilder with method pointer in C# 9
        private static DynamicInvoker Unreflect<TMethod>(TMethod method, Func<Expression?, TMethod, IEnumerable<Expression>, Expression> resultBuilder)
            where TMethod : MethodBase
        {
            var target = Expression.Parameter(typeof(object));
            var arguments = Expression.Parameter(typeof(object[]));
            Expression? thisArg;
            if (method.IsStatic || method.MemberType == MemberTypes.Constructor)
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
                Expression argument = Expression.ArrayAccess(arguments, Expression.Constant(parameter.Position));
                if (parameter.ParameterType.IsByRefLike)
                {
                    throw new NotSupportedException();
                }
                else if (parameter.ParameterType.IsByRef)
                {
                    var parameterType = parameter.ParameterType.GetElementType();

                    // value type parameter can be passed as unboxed reference
                    if (parameterType.IsValueType)
                    {
                        argument = Expression.Unbox(argument, parameterType);
                    }
                    else
                    {
                        var tempVar = Expression.Variable(parameterType);
                        tempVars.Add(tempVar);
                        prologue.Add(Expression.Assign(tempVar, parameterType.IsPointer ? Unwrap(argument, parameterType) : Expression.Convert(argument, parameterType)));
                        if (parameterType.IsPointer)
                            epilogue.Add(Expression.Assign(argument, Wrap(tempVar)));
                        else
                            epilogue.Add(Expression.Assign(argument, tempVar));
                        argument = tempVar;
                    }
                }
                else if (parameter.ParameterType.IsPointer)
                {
                    argument = Unwrap(argument, parameter.ParameterType);
                }
                else
                {
                    argument = Expression.Convert(argument, parameter.ParameterType);
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