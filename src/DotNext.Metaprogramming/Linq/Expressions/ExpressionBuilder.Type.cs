using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

using Collections.Generic;

partial class ExpressionBuilder
{
    /// <summary>
    /// Extends <see cref="Type"/> type.
    /// </summary>
    /// <param name="type">The type to extend.</param>
    extension(Type type)
    {
        /// <summary>
        /// Constructs type default value supplier.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>default(T)</c>.
        /// </remarks>
        /// <value>The type default value expression.</value>
        public DefaultExpression DefaultExpr => Expression.Default(type);
        
        /// <summary>
        /// Constructs type instantiation expression.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>new T()</c>.
        /// </remarks>
        /// <param name="args">The list of arguments to be passed into constructor.</param>
        /// <returns>Instantiation expression.</returns>
        public NewExpression New(params IReadOnlyCollection<Expression> args)
        {
            if (args.Count is 0)
                return Expression.New(type);

            return type.GetConstructor(args.Select(ExpressionBuilder.GetType).ToArray()) is { } ctor
                ? Expression.New(ctor, args)
                : throw new MissingMethodException(type.FullName, ConstructorInfo.ConstructorName);
        }
    }
}