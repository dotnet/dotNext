using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Linq.Expressions.ExpressionBuilder;

    internal sealed class TryCatchFinallyStatement : GuardedStatement
    {
        private readonly uint previousState;
        private readonly uint recoveryStateId;
        private readonly LabelTarget? finallyLabel;

        internal TryCatchFinallyStatement(TryExpression expression, IDictionary<uint, StateTransition> transitionTable, uint previousState, ref uint stateId)
            : base(expression, Label("fault_" + (++stateId)))
        {
            prologue.AddFirst(new EnterGuardedCodeExpression(stateId));
            this.previousState = previousState;
            transitionTable[stateId] = new StateTransition(null, FaultLabel);
            if (expression.Handlers.Count > 0)
            {
                recoveryStateId = ++stateId;
                finallyLabel = Label("finally_" + stateId);
                transitionTable[recoveryStateId] = new StateTransition(null, finallyLabel);
            }
        }

        internal new TryExpression Content => (TryExpression)base.Content;

        internal Expression InlineFinally(ExpressionVisitor visitor, StatePlaceholderExpression leavingState)
        {
            var finallyCode = Content.Finally;
            finallyCode = finallyCode is null ?
                new ExitGuardedCodeExpression(leavingState, false) :
                finallyCode.AddEpilogue(false, new ExitGuardedCodeExpression(leavingState, true));
            finallyCode = finallyCode.AddEpilogue(false, epilogue);
            finallyCode = Inliner.Rewrite(finallyCode);
            return visitor.Visit(finallyCode);
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            // generate try block
            var tryBody = visitor.Visit(Wrap(Content.Body));
            tryBody = tryBody.AddPrologue(false, prologue);
            if (!(finallyLabel is null))
                tryBody = tryBody.AddEpilogue(false, finallyLabel.Goto(), FaultLabel.LandingSite());

            // generate exception handlers block
            var handlers = new LinkedList<Expression>();
            if (finallyLabel != null)
            {
                handlers.AddLast(new ExitGuardedCodeExpression(previousState, false));
                handlers.AddLast(new EnterGuardedCodeExpression(recoveryStateId));
                foreach (var handler in Content.Handlers)
                    handlers.AddLast(visitor.Visit(new CatchStatement(handler, finallyLabel)));
            }

            // generate finally block
            Expression fault = new FinallyStatement(Content.Finally ?? Content.Fault, previousState, finallyLabel ?? FaultLabel);
            fault = visitor.Visit(fault);
            return tryBody.AddEpilogue(false, handlers).AddEpilogue(false, fault);
        }
    }
}
