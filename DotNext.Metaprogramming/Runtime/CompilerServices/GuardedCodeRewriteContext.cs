using System;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using static Metaprogramming.Expressions;

    internal sealed class GuardedCodeRewriteContext
    {
        private readonly EnterGuardedCodeExpression enterGuardedCode;
        private readonly ExitGuardedCodeExpression exitGuardedCode;
        internal readonly LabelTarget FailureLabel;
        internal readonly LabelTarget ExitLabel;

        internal GuardedCodeRewriteContext(uint previousStateId, uint stateId, LabelTarget exitTryCatchLabel, LabelTarget failureLabel)
        {
            enterGuardedCode = new EnterGuardedCodeExpression(stateId);
            exitGuardedCode = new ExitGuardedCodeExpression(previousStateId);
            FailureLabel = failureLabel;
            ExitLabel = exitTryCatchLabel;
        }

        internal BlockExpression MakeTryBody(Expression @try, Expression @finally, ExpressionVisitor visitor)
        {
            @finally = visitor.Visit(SemanticCopyRewriter.Rewrite(@finally));
            return @finally is null ?
                Expression.Block(typeof(void), enterGuardedCode, @try, exitGuardedCode, ExitLabel.Goto()) :
                Expression.Block(typeof(void), enterGuardedCode, @try, exitGuardedCode, @finally, ExitLabel.Goto());
        }

        internal BlockExpression MakeFaultBody(Expression fault, ExpressionVisitor visitor)
        {
            fault = visitor.Visit(SemanticCopyRewriter.Rewrite(@fault));
            return fault is null ?
                Expression.Block(typeof(void), FailureLabel.LandingSite(), exitGuardedCode, Expression.Rethrow()) :
                Expression.Block(typeof(void), FailureLabel.LandingSite(), exitGuardedCode, fault, Expression.Rethrow());
        }

        internal ConditionalExpression MakeCatchBlock(CatchBlock @catch, Expression @finally, ExpressionVisitor visitor)
        {
            @finally = visitor.Visit(SemanticCopyRewriter.Rewrite(@finally));
            var handler = visitor.Visit(@catch.Body);
            var filter = visitor.Visit(@catch.Filter);
            if (VisitorContext.ContainsAwait(filter))
                throw new NotSupportedException("Filter of catch block cannot contain await expressions");
            return Expression.Condition(Expression.AndAlso(new RecoverFromExceptionExpression(exitGuardedCode, @catch.Variable), filter),
                Expression.Block(typeof(void), handler, @finally, ExitLabel.Goto()),
                Expression.Rethrow(),
                typeof(void));
        }
    }
}