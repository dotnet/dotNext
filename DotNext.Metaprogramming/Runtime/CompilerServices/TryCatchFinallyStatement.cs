using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class TryCatchFinallyStatement: GuardedStatement
    {
        private readonly uint tryStateId;
        private readonly uint previousState;
        private readonly uint recoveryStateId;
        private readonly LabelTarget finallyLabel;

        internal TryCatchFinallyStatement(TryExpression expression, IDictionary<uint, StateTransition> transitionTable, uint previousState, ref uint stateId)
            : base(expression, Label("fault_" + (++stateId)))
        {
            this.previousState = previousState;
            tryStateId = stateId;
            transitionTable[tryStateId] = new StateTransition(null, FaultLabel);
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
            tryBody = finallyLabel is null ?
                tryBody.AddPrologue(false, new EnterGuardedCodeExpression(tryStateId)) :
                tryBody.AddPrologue(false, new EnterGuardedCodeExpression(tryStateId)).AddEpilogue(false, finallyLabel.Goto(), FaultLabel.LandingSite());
            //generate exception handlers block
            ICollection<Expression> handlers = new LinkedList<Expression>();
            if (Content.Handlers.Count > 0)
            {
                handlers.Add(new ExitGuardedCodeExpression(previousState));
                handlers.Add(new EnterGuardedCodeExpression(recoveryStateId));
                foreach (var handler in Content.Handlers)
                    handlers.Add(visitor.Visit(new CatchStatement(handler, finallyLabel)));
            }
            //generate finally block
            var fault = visitor.Visit(Content.Finally ?? Content.Fault);
            fault = fault is null ?
                Block(typeof(void), (finallyLabel ?? FaultLabel).LandingSite(), new ExitGuardedCodeExpression(previousState), new RethrowExpression()) :
                fault.AddPrologue(false, (finallyLabel ?? FaultLabel).LandingSite(), new ExitGuardedCodeExpression(previousState)).AddEpilogue(false, new RethrowExpression());
            return tryBody.AddEpilogue(false, handlers).AddEpilogue(false, fault);
        }
    }
}
