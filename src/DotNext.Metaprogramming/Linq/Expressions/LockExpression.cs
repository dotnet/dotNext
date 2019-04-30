using System;
using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Linq.Expressions
{
    using VariantType;

    public sealed class LockExpression: Expression
    {
        public delegate Expression Statement(ParameterExpression syncRoot);

        private readonly BinaryExpression assignment;
        private Expression body;

        private LockExpression(Expression syncRoot, Variant<Expression, Statement> body)
        {
            if (syncRoot is ParameterExpression syncVar)
                SyncRoot = syncVar;
            else
            {
                SyncRoot = Variable(typeof(object), "syncRoot");
                assignment = Assign(SyncRoot, syncRoot);
            }
            if (body.First.TryGet(out var expr))
                this.body = expr;
            else if (body.Second.TryGet(out var statement))
                this.body = statement(SyncRoot);
            else
                this.body = null;
        }

        public LockExpression(Expression syncRoot, Expression body)
            : this(syncRoot, new Variant<Expression, Statement>(body))
        {
        }

        public LockExpression(Expression syncRoot, Statement body)
            : this(syncRoot, new Variant<Expression, Statement>(body))
        {
        }

        internal LockExpression(Expression syncRoot)
            : this(syncRoot, new Variant<Expression, Statement>())
        {
        }

        public ParameterExpression SyncRoot { get;  }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public override bool CanReduce => true;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => typeof(void);

        public override Expression Reduce()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = TryFinally(Body, Call(monitorExit, SyncRoot));
            return assignment is null ?
                    Block(typeof(void), Call(monitorEnter, SyncRoot), body) :
                    Block(typeof(void), Sequence.Singleton(SyncRoot), assignment, Call(monitorEnter, SyncRoot), body);
        }
    }
}