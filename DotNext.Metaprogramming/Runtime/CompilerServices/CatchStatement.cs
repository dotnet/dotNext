using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class CatchStatement : GuardedStatement
    {
        internal readonly ParameterExpression ExceptionVar;
        private readonly Expression filter;

        internal CatchStatement(CatchBlock handler, LabelTarget faultLabel)
            : base(handler.Body, faultLabel)
        {
            var recovery = new RecoverFromExceptionExpression(handler.Variable is null ? Variable(typeof(Exception), "e") : handler.Variable);
            filter = handler.Filter is null ? recovery.Upcast<Expression, RecoverFromExceptionExpression>() : recovery.AndAlso(handler.Filter);
            ExceptionVar = recovery.Receiver;
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var filter = visitor.Visit(this.filter);
            if (ExpressionAttributes.Get(filter)?.ContainsAwait ?? false)
                throw new NotSupportedException("Filter of catch block cannot contain await expressions");
            var handler = visitor.Visit(Content);
            handler = handler.AddPrologue(false, prologue).AddEpilogue(false, epilogue).AddEpilogue(false, FaultLabel.Goto());
            return IfThen(filter, handler);
        }
    }
}
