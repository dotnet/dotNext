using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    using static Reflection.DisposableType;

    public sealed class UsingExpression: Expression
    {
        public delegate Expression Statement(ParameterExpression resource);

        private readonly MethodInfo disposeMethod;
        private readonly BinaryExpression assignment;
        private Expression body;

        internal UsingExpression(Expression resource)
        {
            disposeMethod = resource.Type.GetDisposeMethod() ?? throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(resource.Type));
            if(resource is ParameterExpression param)
            {
                assignment = null;
                Resource = param;
            }
            else
                assignment = Assign(Resource = Expression.Variable(resource.Type, "resource"), resource);
        }

        public UsingExpression(Expression resource, Statement body)
            : this(resource)
        {
            this.body = body(Resource);
        }

        public UsingExpression(Expression resource, Expression body)
            : this(resource)
        {
            this.body = body;
        }

        /// <summary>
        /// Gets body of <see langword="using"/> expression.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public ParameterExpression Resource { get; }

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => Body.Type;

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            if(assignment is null)
                return TryFinally(Body, Block(typeof(void), Call(Resource, disposeMethod), Assign(Resource, Default(Resource.Type))));
            else
                return Block(Sequence.Singleton(Resource), assignment, TryFinally(Body, Call(Resource, disposeMethod)));
        }
    }
}