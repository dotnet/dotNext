using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    public sealed class SwitchBuilder : ExpressionBuilder<SwitchExpression>
    {
        private readonly Expression switchValue;
        private readonly ICollection<SwitchCase> cases;
        private Expression defaultExpression;

        internal SwitchBuilder(Expression expression, ExpressionBuilder parent, bool treatAsStatement)
            : base(parent, treatAsStatement)
        {
            cases = new LinkedList<SwitchCase>();
            defaultExpression = Expression.Empty();
            switchValue = expression;
        }

        public SwitchBuilder Case(IEnumerable<Expression> testValues, Action<ExpressionBuilder> body)
        {
            cases.Add(Expression.SwitchCase(NewScope().Build(body), testValues));
            return this;
        }

        public SwitchBuilder Case(IEnumerable<UniversalExpression> testValues, UniversalExpression body)
        {
            cases.Add(Expression.SwitchCase(body, UniversalExpression.AsExpressions(testValues)));
            return this;
        }

        public SwitchBuilder Case(IEnumerable<UniversalExpression> testValues, Action<ExpressionBuilder> body)
            => Case(UniversalExpression.AsExpressions(testValues), body);

        public SwitchBuilder Case(UniversalExpression test, Action<ExpressionBuilder> body)
            => Case(Sequence.Singleton((Expression)test), body);

        public SwitchBuilder Case(UniversalExpression test, UniversalExpression body)
            => Case(Sequence.Singleton(test), body);

        public SwitchBuilder Default(UniversalExpression body)
        {
            defaultExpression = body;
            return this;
        }

        public SwitchBuilder Default(Action<ExpressionBuilder> body)
            => Default(NewScope().Build(body));

        private protected override SwitchExpression Build()
            => Expression.Switch(ExpressionType, switchValue, defaultExpression, null, cases);
    }
}
