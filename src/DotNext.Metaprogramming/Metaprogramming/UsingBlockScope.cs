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
    internal sealed class UsingBlockScope: LexicalScope, IExpressionBuilder<Expression>, ICompoundStatement<Action<ParameterExpression>>
    {
        private readonly MethodInfo disposeMethod;
        private readonly ParameterExpression resource;
        private readonly BinaryExpression assignment;

        internal UsingBlockScope(Expression expression, LexicalScope parent = null)
            : base(parent)
        {
            disposeMethod = expression.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(expression.Type));
            else if (expression is ParameterExpression variable)
                resource = variable;
            else
            {
                resource = Expression.Variable(expression.Type, "resource");
                assignment = resource.Assign(expression);
            }
        }

        public new Expression Build()
        {
            Expression @finally = resource.Call(disposeMethod);
            @finally = Expression.Block(typeof(void), @finally, resource.AssignDefault());
            @finally = base.Build().Finally(@finally);
            return assignment is null ?
                @finally :
                Expression.Block(typeof(void), Sequence.Singleton(resource), assignment, @finally);
        }

        void ICompoundStatement<Action<ParameterExpression>>.ConstructBody(Action<ParameterExpression> body) => body(resource);
    }
}
