using System;
using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents statement which acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. 
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
    internal sealed class LockScope: LexicalScope, IExpressionBuilder<BlockExpression>, ICompoundStatement<Action<ParameterExpression>>
    {
        private readonly ParameterExpression syncRoot;
        private readonly BinaryExpression assignment;

        internal LockScope(Expression syncRoot, LexicalScope parent)
            : base(parent)
        {
            if(syncRoot is ParameterExpression syncVar)
                this.syncRoot = syncVar;
            else
            {
                this.syncRoot = Expression.Variable(typeof(object), "syncRoot");
                assignment = this.syncRoot.Assign(syncRoot);
            }
        }

        public new BlockExpression Build()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = base.Build();
            if(assignment is null)
            {
                body = body.Finally(Expression.Call(monitorExit, syncRoot));
                return Expression.Block(typeof(void), Expression.Call(monitorEnter, syncRoot), body);
            }
            else
            {
                body = body.Finally(Expression.Block(Expression.Call(monitorExit, syncRoot), syncRoot.AssignDefault()));
                return Expression.Block(typeof(void), Sequence.Singleton(syncRoot), assignment, Expression.Call(monitorEnter, syncRoot), body);
            }
        }

        void ICompoundStatement<Action<ParameterExpression>>.ConstructBody(Action<ParameterExpression> body) => body(syncRoot);
    }
}