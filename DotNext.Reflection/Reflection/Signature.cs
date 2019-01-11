using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.ObjectModel;

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
    }
}