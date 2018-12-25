using System;
using System.Reflection;
using System.Linq.Expressions;
using System.Collections.ObjectModel;

namespace MissingPieces.Metaprogramming
{
    /// <summary>
    /// Describes signature of function or procedure.
    /// </summary>
    /// <typeparam name="A">Method parameters.</typeparam>
    internal static class Signature<A>
        where A : struct
    {
        internal static (Type[] Parameters, Expression[] ArgList, ParameterExpression ArgListParameter) Reflect()
        {
            var argListParameter = Expression.Parameter(typeof(A).MakeByRefType(), "arguments");
            var publicFields = typeof(A).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var parameters = new Type[publicFields.LongLength];
            var arglist = new Expression[publicFields.LongLength];
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

            return (
                Parameters: parameters,
                ArgList: arglist,
                ArgListParameter: argListParameter
            );
        }
    }
}