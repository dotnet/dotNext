using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

namespace DotNext.Linq.Expressions;

using Reflection;

partial class ExpressionBuilder
{
    /// <summary>
    /// Extends <see cref="Expression"/> with properties.
    /// </summary>
    /// <param name="expression">The expression to extend.</param>
    extension(Expression expression)
    {
        /// <summary>
        /// Constructs <see langword="null"/> check.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>a is null</c>.
        /// </remarks>
        /// <value><see langword="null"/> check operation.</value>
        public Expression IsNull
        {
            get
            {
                // handle nullable value type
                var underlyingType = Nullable.GetUnderlyingType(expression.Type);
                if (underlyingType is not null)
                    return !expression.Property(nameof(Nullable<>.HasValue));

                // handle optional type
                underlyingType = Optional.GetUnderlyingType(expression.Type);
                if (underlyingType is not null)
                    return !expression.Property(nameof(Optional<>.HasValue));

                // handle reference type or value type
                return expression.Type is { IsValueType: false, IsPointer: false, IsPrimitive: false }
                    ? Expression.ReferenceEqual(expression, Expression.Constant(null, expression.Type))
                    : false.Quoted;
            }
        }

        /// <summary>
        /// Constructs <see langword="null"/> check.
        /// </summary>
        /// <remarks>
        /// The equivalent code is <c>!(a is null)</c>.
        /// </remarks>
        /// <value><see langword="null"/> check operation.</value>
        public Expression IsNotNull
        {
            get
            {
                // handle nullable value type
                var underlyingType = Nullable.GetUnderlyingType(expression.Type);
                if (underlyingType is not null)
                    return expression.Property(nameof(Nullable<>.HasValue));

                // handle optional type
                underlyingType = Optional.GetUnderlyingType(expression.Type);
                if (underlyingType is not null)
                    return expression.Property(nameof(Optional<>.HasValue));

                // handle reference type or value type
                return expression.Type is { IsValueType: false, IsPointer: false, IsPrimitive: false }
                    ? Expression.ReferenceNotEqual(expression, Expression.Constant(null, expression.Type))
                    : true.Quoted;
            }
        }

        /// <summary>
        /// Converts value type to the expression of <see cref="Nullable{T}"/> type.
        /// </summary>
        /// <remarks>
        /// If <paramref name="expression"/> is of pointer of reference type then
        /// method returns unmodified expression.
        /// </remarks>
        /// <value>The nullable expression.</value>
        public Expression Nullable
            => Nullable.GetUnderlyingType(expression.Type) is null && expression.Type is { IsPointer: false, IsValueType: true }
                ? Expression.Convert(expression, typeof(Nullable<>).MakeGenericType(expression.Type))
                : expression;
        
        /// <summary>
        /// Creates the expression of <see cref="Optional{T}"/> type.
        /// </summary>
        /// <value>The expression of <see cref="Optional{T}"/> type.</value>
        public Expression Optional
            => Expression.Convert(expression, typeof(Optional<>).MakeGenericType(expression.Type));
        
        /// <summary>
        /// Gets a call to <see cref="Debugger.Break"/> as an expression tree.
        /// </summary>
        public static MethodCallExpression Breakpoint => CallStatic(typeof(Debugger), nameof(Debugger.Break));

        /// <summary>
        /// Constructs expression representing count of items in the collection or string.
        /// </summary>
        /// <remarks>
        /// The input expression must be of type <see cref="string"/>, <see cref="StringBuilder"/>, array or any type
        /// implementing <see cref="ICollection{T}"/> or <see cref="IReadOnlyCollection{T}"/>.
        /// </remarks>
        /// <returns>The expression providing access to the appropriate property indicating the number of items in the collection.</returns>
        public Expression CollectionLength
        {
            get
            {
                if (expression.Type.IsArray)
                    return Expression.ArrayLength(expression);

                if (expression.Type == typeof(string) || expression.Type == typeof(StringBuilder))
                    return Expression.Property(expression, nameof(string.Length));

                var interfaceType = expression.Type.ImplementedCollection ??
                                    throw new ArgumentException(ExceptionMessages.CollectionImplementationExpected);
                return Expression.Property(expression, interfaceType, nameof(ICollection<>.Count));
            }
        }
    }
}