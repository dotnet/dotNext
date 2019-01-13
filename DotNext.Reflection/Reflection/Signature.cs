using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.ObjectModel;
using System.Collections.Generic;

namespace DotNext.Reflection
{
    /// <summary>
    /// Describes signature of function or procedure.
    /// </summary>
    /// <typeparam name="A">Method parameters.</typeparam>
    internal static class Signature
    {
        private static void Reflect(ParameterExpression argListParameter, out Type[] parameters, out Expression[] arglist)
        {
            var publicFields = argListParameter.Type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            parameters = new Type[publicFields.LongLength];
            arglist = new Expression[publicFields.LongLength];
            for(var i = 0L; i < publicFields.LongLength; i++)
            {
                var field = publicFields[i];
                if(Ref.Reflect(field.FieldType, out var underlyingType, out var valueField))
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
            where A: struct
            => Reflect(typeof(A));

        private static Expression NormalizeParameter(Type actualParameter, Expression expectedParameter, out ParameterExpression localVar, out Expression postExpression)
        {
            if(actualParameter.IsImplicitlyConvertibleFrom(expectedParameter.Type))
            {
                postExpression = localVar = null;
                return expectedParameter;
            }
            else if(expectedParameter.Type == typeof(object))
                if(actualParameter.IsByRef)
                {
                    //T local = args.param is null ? default(T) : (T)args;
                    //...call(ref local)
                    //args.param = (object)local;
                    localVar = Expression.Variable(actualParameter.GetElementType());
                    postExpression = localVar.Type.IsValueType ?
                        Expression.Assign(expectedParameter, Expression.Convert(localVar, expectedParameter.Type)):
                        Expression.Assign(expectedParameter, localVar);
                    postExpression = Expression.Assign(expectedParameter, Expression.Convert(localVar, expectedParameter.Type));
                    return Expression.Assign(localVar, Expression.Condition(Expression.ReferenceEqual(expectedParameter, Expression.Constant(null, expectedParameter.Type)), 
                        Expression.Default(actualParameter.GetElementType()),
                        Expression.Convert(expectedParameter, actualParameter.GetElementType())));
                }
                else
                {
                    postExpression = localVar = null;
                    return Expression.Condition(Expression.ReferenceEqual(expectedParameter, Expression.Constant(null, expectedParameter.Type)), 
                        Expression.Default(actualParameter),
                        Expression.Convert(expectedParameter, actualParameter));
                }
            else if(actualParameter.IsByRef)
                {
                    postExpression = localVar = null;
                    return expectedParameter;
                }
            else 
            {
                postExpression = localVar = null;
                return Expression.Convert(expectedParameter, actualParameter);
            }
        }

        internal static bool NormalizeParameters(Type[] actualParameters, Expression[] expectedParameters, ICollection<ParameterExpression> locals, ICollection<Expression> postExpressions)
        {
            if(actualParameters.LongLength != expectedParameters.LongLength)
                return false;
            for(var i = 0L; i < actualParameters.LongLength; i++)
                if((expectedParameters[i] = NormalizeParameter(actualParameters[i], expectedParameters[i], out var localVar, out var postExpr)) is null)
                    return false;
                else if(!(postExpr is null) && !(localVar is null))
                {
                    locals.Add(localVar);
                    postExpressions.Add(postExpr);
                }
            return true;
        }
    }
}