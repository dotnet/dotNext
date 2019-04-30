using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForExpression = Linq.Expressions.ForExpression;

    internal sealed class ForStatement : LexicalScope, ILexicalScope<ForExpression, Action<ParameterExpression>>, ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>
    {
        private readonly Action<ParameterExpression> iteration;
        private readonly Func<ParameterExpression, Expression> condition;
        private readonly Expression initialization;

        internal ForStatement(Expression initialization, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, LexicalScope parent)
            : base(parent)
        {
            this.iteration = iteration;
            this.condition = condition;
            this.initialization = initialization;
        }

        ForExpression ILexicalScope<ForExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new ForExpression(initialization, condition);
            scope(result.LoopVar);
            AddStatement(Expression.Continue(result.ContinueLabel));
            iteration(result.LoopVar);
            result.Body = Build();
            return result;
        }

        ForExpression ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>.Build(Action<ParameterExpression, LoopContext> scope)
        {
            var result = new ForExpression(initialization, condition);
            scope(result.LoopVar, new LoopContext(result));
            AddStatement(Expression.Continue(result.ContinueLabel));
            iteration(result.LoopVar);
            result.Body = Build();
            return result;
        }
    }
}
