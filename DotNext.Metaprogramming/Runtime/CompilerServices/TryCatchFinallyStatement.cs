using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class TryCatchFinallyStatement: GuardedStatement
    {
        private readonly uint previousState;
        private readonly uint recoveryStateId;
        private readonly LabelTarget finallyLabel;

        internal TryCatchFinallyStatement(TryExpression expression, IDictionary<uint, StateTransition> transitionTable, uint previousState, ref uint stateId)
            : base(expression, Label("fault_" + (++stateId)))
        {
            prologue.AddFirst(new EnterGuardedCodeExpression(stateId));
            epilogue.AddFirst(new RethrowExpression());
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

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            //generate try block
            var tryBody = visitor.Visit(Wrap(Content.Body));
            tryBody = tryBody.AddPrologue(false, prologue);
            if (!(finallyLabel is null))
                tryBody = tryBody.AddEpilogue(false, finallyLabel.Goto(), FaultLabel.LandingSite());
            //generate exception handlers block
            var handlers = new LinkedList<Expression>();
            if (Content.Handlers.Count > 0)
            {
                handlers.AddLast(new ExitGuardedCodeExpression(previousState));
                handlers.AddLast(new EnterGuardedCodeExpression(recoveryStateId));
                foreach (var handler in Content.Handlers)
                    handlers.AddLast(visitor.Visit(new CatchStatement(handler, finallyLabel)));
            }
            //generate finally block
            var fault = visitor.Visit(Content.Finally ?? Content.Fault);
            fault = fault is null ?
                Block(typeof(void), (finallyLabel ?? FaultLabel).LandingSite(), new ExitGuardedCodeExpression(previousState)) :
                fault.AddPrologue(false, (finallyLabel ?? FaultLabel).LandingSite(), new ExitGuardedCodeExpression(previousState));
            fault = fault.AddEpilogue(false, epilogue);
            return tryBody.AddEpilogue(false, handlers).AddEpilogue(false, fault);
        }
    }
}
