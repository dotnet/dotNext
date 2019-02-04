using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class RecoveryCodeContext : GuardedCodeContext
    {
        internal RecoveryCodeContext(uint previousState, uint guardedState, LabelTarget faultLabel) 
            : base(previousState, guardedState, faultLabel)
        {
        }

        internal ConditionalExpression MakeCatchBlock(CatchBlock @catch, IDictionary<uint, StateTransition> stateSwitchTable, ExpressionVisitor visitor)
        {
            RegisterFaultTransition(stateSwitchTable);
            @catch = @catch.Filter is null ?
                @catch.Update(@catch.Variable, new RecoverFromExceptionExpression(@catch.Variable), @catch.Body) :
                @catch.Update(@catch.Variable, new RecoverFromExceptionExpression(@catch.Variable).AndAlso(@catch.Filter), @catch.Body);
            var filter = visitor.Visit(@catch.Filter);
            if (VisitorContext.ContainsAwait(filter))
                throw new NotSupportedException("Filter of catch block cannot contain await expressions");
            var handler = visitor.Visit(@catch.Body);
            handler = Expression.Block(typeof(void), exitGuardedCode, enterGuardedCode, handler, FaultLabel.Goto());
            return Expression.IfThen(filter, handler);
        }
    }
}
