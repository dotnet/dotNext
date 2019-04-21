using System;
using MethodInfo = System.Reflection.MethodInfo;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using static Reflection.DisposableType;

    /// <summary>
    /// Represents <see langword="using"/> statement builder.
    /// </summary>
    /// 
    internal sealed class UsingBlockBuilder: ScopeBuilder, IExpressionBuilder<Expression>
    {
        private readonly MethodInfo disposeMethod;
        internal readonly ParameterExpression Resource;
        private readonly BinaryExpression assignment;

        internal UsingBlockBuilder(Expression expression, LexicalScope parent = null)
            : base(parent)
        {
            disposeMethod = expression.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(expression.Type));
            else if (expression is ParameterExpression variable)
                Resource = variable;
            else
            {
                Resource = Expression.Variable(expression.Type, "resource");
                assignment = Resource.Assign(expression);
            }
        }

        public new Expression Build()
        {
            Expression @finally = Resource.Call(disposeMethod);
            @finally = Expression.Block(typeof(void), @finally, Resource.AssignDefault());
            @finally = base.Build().Finally(@finally);
            return assignment is null ?
                @finally :
                Expression.Block(typeof(void), Sequence.Singleton(Resource), assignment, @finally);
        }
    }
}
