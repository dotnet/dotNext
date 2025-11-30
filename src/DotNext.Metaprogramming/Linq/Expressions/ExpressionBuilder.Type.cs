using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

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
        public NewExpression New(params Expression[] args)
        {
            if (args.LongLength is 0L)
                return Expression.New(type);

            return type.GetConstructor(Array.ConvertAll(args, static arg => arg.Type)) is { } ctor
                ? Expression.New(ctor, args)
                : throw new MissingMethodException(type.FullName, ConstructorInfo.ConstructorName);
        }
    }
}