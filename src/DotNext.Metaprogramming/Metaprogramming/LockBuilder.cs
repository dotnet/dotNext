using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents statement which acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. 
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
    internal sealed class LockBuilder: ScopeBuilder, IExpressionBuilder<BlockExpression>
    {
        internal readonly ParameterExpression SyncRoot;
        private readonly BinaryExpression assignment;

        internal LockBuilder(Expression syncRoot, LexicalScope parent)
            : base(parent)
        {
            if(syncRoot is ParameterExpression syncVar)
                this.SyncRoot = syncVar;
            else
            {
                this.SyncRoot = Expression.Variable(typeof(object), "syncRoot");
                assignment = this.SyncRoot.Assign(syncRoot);
            }
        }

        public new BlockExpression Build()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = base.Build();
            if(assignment is null)
            {
                body = body.Finally(Expression.Call(monitorExit, SyncRoot));
                return Expression.Block(typeof(void), Expression.Call(monitorEnter, SyncRoot), body);
            }
            else
            {
                body = body.Finally(Expression.Block(Expression.Call(monitorExit, SyncRoot), SyncRoot.AssignDefault()));
                return Expression.Block(typeof(void), Sequence.Singleton(SyncRoot), assignment, Expression.Call(monitorEnter, SyncRoot), body);
            }
        }
    }
}