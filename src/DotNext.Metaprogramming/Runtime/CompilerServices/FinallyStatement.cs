using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class FinallyStatement : Statement
    {
        internal FinallyStatement(Expression body, uint previousState, LabelTarget finallyLabel)
            : base(body)
        {
            prologue.AddFirst(finallyLabel.LandingSite());
            prologue.AddLast(new ExitGuardedCodeExpression(previousState));
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
            => visitor.Visit(Content).AddPrologue(false, prologue).AddEpilogue(false, epilogue).AddEpilogue(false, new RethrowExpression());
    }
}
