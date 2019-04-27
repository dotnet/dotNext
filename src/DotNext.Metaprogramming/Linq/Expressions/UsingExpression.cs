using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    using static Reflection.DisposableType;

    public sealed class UsingExpression: Expression
    {
        private readonly MethodInfo disposeMethod;
        private readonly BinaryExpression assignment;

        public UsingExpression(Expression resource, Func<ParameterExpression, Expression> body = null)
        {
            disposeMethod = resource.Type.GetDisposeMethod();
            if (disposeMethod is null)
                throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(resource.Type));
            else if (resource is ParameterExpression variable)
                Resource = variable;
            else
            {
                Resource = Variable(resource.Type, "resource");
                assignment = Assign(Resource, resource);
            }
            Body = body?.Invoke(Resource) ?? Empty();
        }

        private UsingExpression(UsingExpression other, Expression body)
        {
            disposeMethod = other.disposeMethod;
            assignment = other.assignment;
            Resource = other.Resource;
            Body = body;
        }

        /// <summary>
        /// Gets body of <see langword="using"/> expression.
        /// </summary>
        public Expression Body { get; }

        public ParameterExpression Resource { get; }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public UsingExpression Update(Expression body) => new UsingExpression(this, body);

        public override Expression Reduce()
        {
            Expression @finally = Call(Resource, disposeMethod);
            @finally = Block(typeof(void), @finally, Assign(Resource, Default(Resource.Type)));
            @finally = TryFinally(Body, @finally);
            return assignment is null ?
                @finally :
                Expression.Block(typeof(void), Sequence.Singleton(Resource), assignment, @finally);
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var newBody = visitor.Visit(Body);
            return ReferenceEquals(Body, newBody) ? this : new UsingExpression(this, newBody);
        }
    }
}