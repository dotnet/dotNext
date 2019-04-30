using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForExpression = Linq.Expressions.ForExpression;

    internal readonly struct ForStatement : IStatement<ForExpression, Action<ParameterExpression>>, IStatement<ForExpression, Action<ParameterExpression, LoopContext>>
    {
        private readonly Action<ParameterExpression> iteration;
        private readonly Func<ParameterExpression, Expression> condition;
        private readonly Expression initialization;

        internal ForStatement(Expression initialization, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration)
        {
            this.iteration = iteration;
            this.condition = condition;
            this.initialization = initialization;
        }

        ForExpression IStatement<ForExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope, ILexicalScope body)
        {
            var result = new ForExpression(initialization, condition);
            scope(result.LoopVar);
            body.AddStatement(Expression.Continue(result.ContinueLabel));
            iteration(result.LoopVar);
            result.Body = body.Build();
            return result;
        }

        ForExpression IStatement<ForExpression, Action<ParameterExpression, LoopContext>>.Build(Action<ParameterExpression, LoopContext> scope, ILexicalScope body)
        {
            var result = new ForExpression(initialization, condition);
            scope(result.LoopVar, new LoopContext(result));
            body.AddStatement(Expression.Continue(result.ContinueLabel));
            iteration(result.LoopVar);
            result.Body = body.Build();
            return result;
        }
    }
}
