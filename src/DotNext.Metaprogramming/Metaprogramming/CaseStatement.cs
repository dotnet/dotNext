using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class CaseStatement : Statement<SwitchBuilder>
    {
        internal readonly struct Factory : IFactory<CaseStatement>
        {
            private readonly SwitchBuilder builder;
            private readonly IEnumerable<Expression> testValues;

            internal Factory(SwitchBuilder builder, IEnumerable<Expression> testValues)
            {
                this.builder = builder;
                this.testValues = testValues;
            }

            public CaseStatement Create(LexicalScope parent) => new CaseStatement(builder, testValues, parent);
        }

        private readonly SwitchBuilder builder;
        private readonly IEnumerable<Expression> testValues;

        private CaseStatement(SwitchBuilder builder, IEnumerable<Expression> testValues, LexicalScope parent)
            : base(parent)
        {
            this.builder = builder;
            this.testValues = testValues;
        }

        private protected override SwitchBuilder CreateExpression(Expression body) => builder.Case(testValues, body);
    }
}
