using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        internal static (Type[] Parameters, Expression[] ArgList, ParameterExpression ArgListParameter) Reflect<TArgs>()
            where TArgs : struct
            => Reflect(typeof(TArgs));

        private static Expression NormalizeOutArgument(Type actualParameter, Expression expectedArgument, out ParameterExpression localVar, out Expression prologue, out Expression epilogue)
        {
            Debug.Assert(actualParameter.IsByRef);
            var elementType = actualParameter.GetElementType();

            // for value type
            // object local = args.param;
            // if (local is null) local = default(T)
            // ...call(ref Unbox<T>(local))
            // args.param = local;
            if (elementType.IsValueType)
            {
                localVar = Expression.Variable(expectedArgument.Type);
                var assignment = Expression.Assign(localVar, expectedArgument);
                var condition = Expression.IfThen(
                    Expression.ReferenceEqual(localVar, Expression.Constant(null, expectedArgument.Type)),
                    Expression.Assign(localVar, Expression.Convert(Expression.Default(elementType), expectedArgument.Type)));
                prologue = Expression.Block(assignment, condition);
                epilogue = Expression.Assign(expectedArgument, localVar);
                return Expression.Unbox(localVar, elementType);
            }

            // for reference type
            // T local = (T)args.param;
            // ...call(ref local)
            // args.param = local
            localVar = Expression.Variable(elementType);
            prologue = Expression.Assign(localVar, Expression.Convert(expectedArgument, elementType));
            epilogue = Expression.Assign(expectedArgument, localVar);

            return localVar;
        }

        private static Expression NormalizeRefArgument(Type actualParameter, Expression expectedArgument, out ParameterExpression? localVar, out Expression? prologue, out Expression? epilogue)
        {
            Debug.Assert(actualParameter.IsByRef);
            var elementType = actualParameter.GetElementType();

            // for value type use unboxed reference to value type
            if (elementType.IsValueType)
            {
                epilogue = prologue = localVar = null;
                return Expression.Unbox(expectedArgument, elementType);
            }

            // for reference type, roundrip via local variable is required
            // the same as for out reference type parameter
            return NormalizeOutArgument(actualParameter, expectedArgument, out localVar, out prologue, out epilogue);
        }

        private static Expression NormalizeArgument(Type actualParameter, Expression expectedArgument, out ParameterExpression? localVar, out Expression? prologue, out Expression? epilogue)
        {
            epilogue = prologue = localVar = null;

            // convert object to the actual type of the parameter
            return actualParameter == expectedArgument.Type ?
                expectedArgument :
                Expression.Convert(expectedArgument, actualParameter);
        }

        private static Expression NormalizeArgument(ParameterInfo actualParameter, Expression expectedArgument, out ParameterExpression? localVar, out Expression? prologue, out Expression? epilogue)
        {
            if (actualParameter.ParameterType.IsAssignableFromWithoutBoxing(expectedArgument.Type))
            {
                epilogue = prologue = localVar = null;
                return expectedArgument;
            }

            if (expectedArgument.Type == typeof(object))
            {
                // out parameters can be passed as null
                if (actualParameter.IsOut)
                    return NormalizeOutArgument(actualParameter.ParameterType, expectedArgument, out localVar, out prologue, out epilogue);

                // ref parameter of value type should never be null
                // ref parameter of reference type can be null
                if (actualParameter.ParameterType.IsByRef)
                    return NormalizeRefArgument(actualParameter.ParameterType, expectedArgument, out localVar, out prologue, out epilogue);

                // regular argument should be always converted to target type
                return NormalizeArgument(actualParameter.ParameterType, expectedArgument, out localVar, out prologue, out epilogue);
            }

            epilogue = prologue = localVar = null;

            if (actualParameter.ParameterType.IsByRef)
                return expectedArgument;

            return Expression.Convert(expectedArgument, actualParameter.ParameterType);
        }

        internal static bool NormalizeArguments(ParameterInfo[] actualParameters, Expression[] expectedArguments, ICollection<ParameterExpression> locals, ICollection<Expression> prologue, ICollection<Expression> epilogue)
        {
            if (actualParameters.LongLength != expectedArguments.LongLength)
                return false;
            for (var i = 0L; i < actualParameters.LongLength; i++)
            {
                if ((expectedArguments[i] = NormalizeArgument(actualParameters[i], expectedArguments[i], out var localVar, out var pro, out var epi)) is null)
                    return false;

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