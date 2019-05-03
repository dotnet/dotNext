using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Reflection
{
    /// <summary>
    /// Describes signature of function or procedure.
    /// </summary>
    internal static class Signature
    {
        private static void Reflect(ParameterExpression argListParameter, out Type[] parameters, out Expression[] arglist)
        {
            var publicFields = argListParameter.Type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            parameters = new Type[publicFields.LongLength];
            arglist = new Expression[publicFields.LongLength];
            for (var i = 0L; i < publicFields.LongLength; i++)
            {
                var field = publicFields[i];
                if (Ref.Reflect(field.FieldType, out var underlyingType, out var valueField))
                {
                    parameters[i] = underlyingType.MakeByRefType();
                    arglist[i] = Expression.Field(Expression.Field(argListParameter, field), valueField);
                }
                else
                {
                    parameters[i] = field.FieldType;
                    arglist[i] = Expression.Field(argListParameter, field);
                }
            }
        }

        internal static (Type[] Parameters, Expression[] ArgList, ParameterExpression ArgListParameter) Reflect(Type argumentsType)
        {
            var argListParameter = argumentsType.IsByRef ?
                Expression.Parameter(argumentsType, "arguments") :
                Expression.Parameter(argumentsType.MakeByRefType(), "arguments");
            Reflect(argListParameter, out var parameters, out var arglist);

            return (Parameters: parameters, ArgList: arglist, ArgListParameter: argListParameter);
        }

        internal static (Type[] Parameters, Expression[] ArgList, ParameterExpression ArgListParameter) Reflect<A>()
            where A : struct
            => Reflect(typeof(A));

        private static Expression NormalizeArgument(Type actualParameter, Expression expectedArgument, out ParameterExpression localVar, out Expression prologue, out Expression epilogue)
        {
            if (actualParameter.IsAssignableFromWithoutBoxing(expectedArgument.Type))
            {
                epilogue = prologue = localVar = null;
                return expectedArgument;
            }
            else if (expectedArgument.Type == typeof(object))
                if (actualParameter.IsByRef)
                {
                    //T local = args.param is null ? default(T) : (T)args;
                    //...call(ref local)
                    //args.param = (object)local;
                    localVar = Expression.Variable(actualParameter.GetElementType());
                    prologue = Expression.Assign(localVar, Expression.Condition(Expression.ReferenceEqual(expectedArgument, Expression.Constant(null, expectedArgument.Type)),
                        Expression.Default(actualParameter.GetElementType()),
                        Expression.Convert(expectedArgument, actualParameter.GetElementType())));
                    epilogue = localVar.Type.IsValueType ?
                        Expression.Assign(expectedArgument, Expression.Convert(localVar, expectedArgument.Type)) :
                        Expression.Assign(expectedArgument, localVar);
                    return localVar;
                }
                else
                {
                    epilogue = prologue = localVar = null;
                    return Expression.Condition(Expression.ReferenceEqual(expectedArgument, Expression.Constant(null, expectedArgument.Type)),
                        Expression.Default(actualParameter),
                        Expression.Convert(expectedArgument, actualParameter));
                }
            else if (actualParameter.IsByRef)
            {
                epilogue = prologue = localVar = null;
                return expectedArgument;
            }
            else
            {
                epilogue = prologue = localVar = null;
                return Expression.Convert(expectedArgument, actualParameter);
            }
        }

        internal static bool NormalizeArguments(Type[] actualParameters, Expression[] expectedArguments, ICollection<ParameterExpression> locals, ICollection<Expression> prologue, ICollection<Expression> epilogue)
        {
            if (actualParameters.LongLength != expectedArguments.LongLength)
                return false;
            for (var i = 0L; i < actualParameters.LongLength; i++)
                if ((expectedArguments[i] = NormalizeArgument(actualParameters[i], expectedArguments[i], out var localVar, out var pro, out var epi)) is null)
                    return false;
                else
                {
                    if (!(localVar is null))
                        locals.Add(localVar);
                    if (!(pro is null))
                        prologue.Add(pro);
                    if (!(epi is null))
                        epilogue.Add(epi);
                }
            return true;
        }
    }
}