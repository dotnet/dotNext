using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using static System.Linq.Enumerable;

namespace DotNext.Runtime.CompilerServices
{
    using VariantType;

    internal sealed class VisitorContext : Stack<Expression>
    {
        private sealed class CodeInsertionPoint
        {
            private Variant<LinkedList<Expression>, LinkedListNode<Expression>> nodeOrList;

            internal CodeInsertionPoint(LinkedList<Expression> list) => nodeOrList = list;
            internal CodeInsertionPoint(LinkedListNode<Expression> node) => nodeOrList = node;

            internal void Insert(Expression expr)
            {
                if (nodeOrList.First.TryGet(out var list))
                    nodeOrList = list.AddLast(expr);
                else if (nodeOrList.Second.TryGet(out var node))
                    nodeOrList = node.List.AddAfter(node, expr);
            }
        }

        private sealed class RewritePoint : Disposable
        {
            private readonly LinkedList<Expression> prologue = new LinkedList<Expression>();
            private readonly LinkedList<Expression> epilogue = new LinkedList<Expression>();

            private static CodeInsertionPoint CaptureRewritePoint(LinkedList<Expression> codeBlock)
                => codeBlock.First is null ? new CodeInsertionPoint(codeBlock) : new CodeInsertionPoint(codeBlock.Last);

            internal CodeInsertionPoint CapturePrologueWriter() => CaptureRewritePoint(prologue);

            internal CodeInsertionPoint CaptureEpilogueWriter() => CaptureRewritePoint(epilogue);

            internal Expression Rewrite(Expression expression)
                => prologue.Count == 0 && epilogue.Count == 0 ?
                        expression :
                        Expression.Block(typeof(void), prologue.Concat(Sequence.Single(expression)).Concat(epilogue));

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    prologue.Clear();
                    epilogue.Clear();
                }
            }
        }

        private static readonly UserDataSlot<RewritePoint> StatementSlot = UserDataSlot<RewritePoint>.Allocate();
        private static readonly UserDataSlot<bool> ContainsAwaitSlot = UserDataSlot<bool>.Allocate();
        private static readonly UserDataSlot<bool> CompilerGeneratedLabelTargetSlot = UserDataSlot<bool>.Allocate();
        private static readonly UserDataSlot<IGuardedCodeContext> GuardedCodeScope = UserDataSlot<IGuardedCodeContext>.Allocate();
        private static readonly UserDataSlot<uint> ExpressionStateSlot = UserDataSlot<uint>.Allocate();
        private static readonly UserDataSlot<ParameterExpression> ExceptionHolderSlot = UserDataSlot<ParameterExpression>.Allocate();

        private static void MarkAsCompilerGenerated(LabelTarget target)
            => target.GetUserData().Set(CompilerGeneratedLabelTargetSlot, true);

        private static bool IsCompilerGenerated(LabelTarget target)
            => target.GetUserData().Get(CompilerGeneratedLabelTargetSlot);

        internal static void IsCompilerGenerated(GotoExpression @goto)
            => IsCompilerGenerated(@goto.Target);

        internal static void IsCompilerGenerated(LabelExpression landingSite)
            => IsCompilerGenerated(landingSite.Target);

        internal static LabelTarget CompilerGeneratedLabelTarget(string name)
        {
            var target = Expression.Label(name);
            MarkAsCompilerGenerated(target);
            return target;
        }

        private static void MarkAsRewritePoint(Expression node) => node.GetUserData().Set(StatementSlot, new RewritePoint());
        
        private uint stateId = AsyncStateMachine<ValueTuple>.FINAL_STATE;
        private uint previousStateId = AsyncStateMachine<ValueTuple>.FINAL_STATE;

        internal new void Push(Expression node)
        {
            node.GetUserData().Set(ExpressionStateSlot, stateId);
            var current = Count == 0 ? null : Peek();
            //block cannot be treated as statement
            if (node is ConditionalExpression conditional && conditional.Type == typeof(void))
            {
                MarkAsRewritePoint(node);
                MarkAsRewritePoint(conditional.IfTrue);
                MarkAsRewritePoint(conditional.IfFalse);
            }
            else if (node is BlockExpression || node is LoopExpression)
            {
                //nothing to do
            }
            else if (node is TryExpression tryCatch)
            {
                var context = tryCatch.Handlers.Count > 0 ?
                    new TryCatchCodeContext(previousStateId, ++stateId, CompilerGeneratedLabelTarget("fault_" + stateId), ++stateId, CompilerGeneratedLabelTarget("finally_" + stateId)) :
                    new TryCatchCodeContext(previousStateId, ++stateId, CompilerGeneratedLabelTarget("finally_" + stateId));
                tryCatch.GetUserData().Set(GuardedCodeScope, context);
                //create context for catch-block
                foreach (var handler in tryCatch.Handlers)
                {
                    handler.Body.GetUserData().Set(ExceptionHolderSlot, handler.Variable);
                    handler.Body.GetUserData().Set(GuardedCodeScope, context.RecoveryContext);
                }
            }
            else if (current is null || current is BlockExpression || current is TryExpression || current is LoopExpression)
                MarkAsRewritePoint(node);
            base.Push(node);
        }

        internal static bool ContainsAwait(Expression expr) => expr.GetUserData().Get(ContainsAwaitSlot, false);

        internal void ContainsAwait()
        {
            foreach (var lookup in this)
                if (lookup is BlockExpression || lookup is TryExpression)
                    return;
                else
                    lookup.GetUserData().Set(ContainsAwaitSlot, true);
        }
        
        internal Expression Pop(Expression node)
        {
            var current = Pop();
            previousStateId = current.GetUserData().Get(ExpressionStateSlot);
            if (current.GetUserData().Remove(StatementSlot, out var replacement))
                using (replacement)
                {
                    node = replacement.Rewrite(node);
                }
            return node;
        }

        internal KeyValuePair<uint, StateTransition> NewTransition(IDictionary<uint, StateTransition> table)
        {
            StackWalk(GuardedCodeScope, out var context);
            stateId += 1;
            var transition = new StateTransition(CompilerGeneratedLabelTarget("state_" + stateId), context?.FaultLabel);
            var pair = new KeyValuePair<uint, StateTransition>(stateId, transition);
            table.Add(pair);
            return pair;
        }

        internal static TryCatchCodeContext RemoveTryCatchContext(TryExpression node)
            => (node.GetUserData().Remove(GuardedCodeScope, out var context) ? context : null) as TryCatchCodeContext ?? throw new InvalidOperationException();
        
        private bool StackWalk<V>(UserDataSlot<V> slot, out V data)
        {
            foreach (var lookup in this)
                if (lookup.GetUserData().Get(slot, out data))
                    return true;
            data = default;
            return false;
        }

        private RewritePoint FindRewritePoint()
            => StackWalk(StatementSlot, out var point) ? point : throw new InvalidOperationException();

        internal Metaprogramming.CodeInsertionPoint GetStatementPrologueWriter() => FindRewritePoint().CapturePrologueWriter().Insert;

        internal Metaprogramming.CodeInsertionPoint GetStatementEpilogueWriter() => FindRewritePoint().CaptureEpilogueWriter().Insert;

        internal ParameterExpression ExceptionHolder => StackWalk(ExceptionHolderSlot, out var holder) ? holder : null;

        internal Expression Rewrite<E>(E expression, Converter<E, Expression> rewriter)
            where E : Expression
        {
            Push(expression);
            var result = rewriter(expression);
            return Pop(result);
        }
    }
}