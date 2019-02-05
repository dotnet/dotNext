using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    using AwaitExpression = Metaprogramming.AwaitExpression;

    internal sealed class VisitorContext : Disposable
    {
        private readonly Stack<ExpressionAttributes> attributes = new Stack<ExpressionAttributes>();
        private readonly Stack<Statement> statements = new Stack<Statement>();
        private uint stateId = AsyncStateMachine<ValueTuple>.FINAL_STATE;
        private uint previousStateId = AsyncStateMachine<ValueTuple>.FINAL_STATE;

        internal Statement CurrentStatement => statements.Peek();

        internal KeyValuePair<uint, StateTransition> NewTransition(IDictionary<uint, StateTransition> table)
        {
            var guardedStmt = FindStatement<GuardedStatement>();
            stateId += 1;
            var transition = new StateTransition(Expression.Label("state_" + stateId), guardedStmt?.FaultLabel);
            var pair = new KeyValuePair<uint, StateTransition>(stateId, transition);
            table.Add(pair);
            return pair;
        }
       
        private S FindStatement<S>()
            where S : Statement
        {
            foreach (var statement in statements)
                if (statement is S result)
                    return result;
            return null;
        }

        internal bool IsInFinally => !(FindStatement<FinallyStatement>() is null);

        internal ParameterExpression ExceptionHolder => FindStatement<CatchStatement>()?.ExceptionVar;

        private void ContainsAwait()
        {
            foreach (var attr in attributes)
                if (ReferenceEquals(ExpressionAttributes.Get(CurrentStatement), attr))
                    return;
                else
                    attr.ContainsAwait = true;
        }

        internal O Rewrite<I, O, A>(I expression, Converter<I, O> rewriter, Action<A> initializer = null)
            where I : Expression
            where O : Expression
            where A : ExpressionAttributes, new()
        {
            var attr = new A() { StateId = stateId };
            initializer?.Invoke(attr);
            attr.AttachTo(expression);

            var isStatement = false;
            if (expression is Statement statement)
            {
                statements.Push(statement);
                isStatement = true;
            }
            else if (expression is AwaitExpression)
            {
                attr.ContainsAwait = true;
                ContainsAwait();
            }
            attributes.Push(attr);
            var result = rewriter(expression);
            attributes.Pop().AttachTo(result);
            if (isStatement)
            {
                statements.Pop();
                previousStateId = attr.StateId;
            }
            return result;
        }

        internal O Rewrite<I, O>(I expression, Converter<I, O> rewriter)
            where I : Expression
            where O : Expression
            => Rewrite<I, O, ExpressionAttributes>(expression, rewriter);

        internal Expression Rewrite(TryExpression expression, IDictionary<uint, StateTransition> transitionTable, Converter<TryCatchFinallyStatement, Expression> rewriter)
        {
            var statement = new TryCatchFinallyStatement(expression, transitionTable, previousStateId, ref stateId);
            return Rewrite(statement, rewriter);
        }

        internal IEnumerable<Expression> FinalizationCode(ExpressionVisitor visitor)
        {
            //iterate through snapshot of statements because collection can be modified
            var statements = new Stack<Statement>(this.statements);
            foreach (var lookup in statements)
                if (lookup is TryCatchFinallyStatement statement)
                    yield return statement.InlineFinally(visitor, 0);
            statements.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                attributes.Clear();
                statements.Clear();
            }
        }
    }
}