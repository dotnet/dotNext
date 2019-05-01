using System;
using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Linq.Expressions
{
    public sealed class LockExpression: Expression
    {
        public delegate Expression Statement(ParameterExpression syncRoot);

        private readonly BinaryExpression assignment;
        private Expression body;

        internal LockExpression(Expression syncRoot)
        {
            if (syncRoot is ParameterExpression syncVar)
                SyncRoot = syncVar;
            else
            {
                SyncRoot = Variable(typeof(object), "syncRoot");
                assignment = Assign(SyncRoot, syncRoot);
            }
        }

        public LockExpression(Expression syncRoot, Expression body)
            : this(syncRoot)
        {
            this.body = body;
        }

        public LockExpression(Expression syncRoot, Statement body)
            : this(syncRoot)
        {
            this.body = body(SyncRoot);
        }

        public ParameterExpression SyncRoot { get;  }

        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        public override bool CanReduce => true;

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override Type Type => Body.Type;

        public override Expression Reduce()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = TryFinally(Body, Call(monitorExit, SyncRoot));
            return assignment is null ?
                    Block(Call(monitorEnter, SyncRoot), body) :
                    Block(Sequence.Singleton(SyncRoot), assignment, Call(monitorEnter, SyncRoot), body);
        }
    }
}