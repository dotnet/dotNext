using System.Linq.Expressions;
using System.Threading;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Represents statement which acquires the mutual-exclusion lock for a given object, executes a statement block, and then releases the lock. 
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/lock-statement">lock Statement</seealso>
    public sealed class LockBuilder: ScopeBuilder, IExpressionBuilder<BlockExpression>
    {
        private readonly ParameterExpression syncRoot;
        private readonly BinaryExpression assignment;

        internal LockBuilder(Expression syncRoot, CompoundStatementBuilder parent)
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

        internal override Expression Build() => Build<BlockExpression, LockBuilder>(this);

        BlockExpression IExpressionBuilder<BlockExpression>.Build()
        {
            var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object) });
            var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            var body = base.Build();
            body = body.Finally(Expression.Call(monitorExit, syncRoot));
            return assignment is null ?
                Expression.Block(typeof(void), Expression.Call(monitorEnter, syncRoot), body) :
                Expression.Block(typeof(void), Sequence.Singleton(syncRoot), assignment, Expression.Call(monitorEnter, syncRoot), body);
        }
    }
}