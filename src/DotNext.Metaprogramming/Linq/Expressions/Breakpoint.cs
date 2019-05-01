using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    internal sealed class Breakpoint : Expression
    {
        internal Breakpoint()
        {
        }

        public override Type Type => typeof(void);

        public override ExpressionType NodeType => ExpressionType.Extension;

        public override bool CanReduce => true;

        public override Expression Reduce() => typeof(Debugger).CallStatic(nameof(Debugger.Break));
    }
}