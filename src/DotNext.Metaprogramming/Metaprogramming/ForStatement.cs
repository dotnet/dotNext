using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using ForExpression = Linq.Expressions.ForExpression;

    internal sealed class ForStatement : LoopLexicalScope, ILexicalScope<ForExpression, Action<ParameterExpression>>, ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>
    {
        internal readonly struct Factory : IFactory<ForStatement>
        {
            private readonly Action<ParameterExpression> iteration;
            private readonly Func<ParameterExpression, Expression> condition;
            private readonly Expression initialization;

            internal Factory(Expression initialization, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration)
            {
                this.iteration = iteration;
                this.condition = condition;
                this.initialization = initialization;
            }

            public ForStatement Create(LexicalScope parent) => new ForStatement(initialization, condition, iteration, parent);
        }

        private readonly Action<ParameterExpression> iteration;
        private readonly Func<ParameterExpression, Expression> condition;
        private readonly Expression initialization;

        private ForStatement(Expression initialization, Func<ParameterExpression, Expression> condition, Action<ParameterExpression> iteration, LexicalScope parent)
            : base(parent)
        {
            this.iteration = iteration;
            this.condition = condition;
            this.initialization = initialization;
        }

        ForExpression ILexicalScope<ForExpression, Action<ParameterExpression>>.Build(Action<ParameterExpression> scope)
        {
            var result = new ForExpression(initialization, ContinueLabel, BreakLabel, condition);
            scope(result.LoopVar);
            AddStatement(Expression.Label(ContinueLabel));
            iteration(result.LoopVar);
            result.Body = Build();
            return result;
        }

        ForExpression ILexicalScope<ForExpression, Action<ParameterExpression, LoopContext>>.Build(Action<ParameterExpression, LoopContext> scope)
        {
            var result = new ForExpression(initialization, ContinueLabel, BreakLabel, condition);
            using(var context = new LoopContext(result))
                scope(result.LoopVar, context);
            AddStatement(Expression.Label(result.ContinueLabel));
            iteration(result.LoopVar);
            result.Body = Build();
            return result;
        }
    }
}
