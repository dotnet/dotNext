using System;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    using VariantType;
    using static Reflection.DisposableType;

    public sealed class UsingExpression: Expression
    {
        public delegate Expression Statement(ParameterExpression resource);

        private readonly MethodInfo disposeMethod;
        private readonly BinaryExpression assignment;
        private Expression body;

        private UsingExpression(Expression resource, Variant<Expression, Statement> body)
        {
            disposeMethod = resource.Type.GetDisposeMethod() ?? throw new ArgumentNullException(ExceptionMessages.DisposePatternExpected(resource.Type));
            if(resource is ParameterExpression param)
            {
                assignment = null;
                Resource = param;
            }
            else
                assignment = Assign(Resource = Expression.Variable(resource.Type, "resource"), resource);
            //construct body
            if (body.First.TryGet(out var expr))
                this.body = expr;
            else if (body.Second.TryGet(out var factory))
                this.body = factory(Resource);
            else
                this.body = null;
        }

        public UsingExpression(Expression resource, Statement body)
            : this(resource, new Variant<Expression, Statement>(body))
        {
        }

        public UsingExpression(Expression resource, Expression body)
            : this(resource, new Variant<Expression, Statement>(body))
        {
        }

        internal UsingExpression(Expression resource)
            : this(resource, new Variant<Expression, Statement>())
        {
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

        public override Type Type => typeof(void);

        public override bool CanReduce => true;

        public override Expression Reduce()
        {
            Expression @finally = Call(Resource, disposeMethod);
            @finally = Block(typeof(void), @finally, Assign(Resource, Default(Resource.Type)));
            @finally = TryFinally(Body, @finally);
            return assignment is null ?
                @finally :
                Expression.Block(typeof(void), Sequence.Singleton(Resource), assignment, @finally);
        }
    }
}