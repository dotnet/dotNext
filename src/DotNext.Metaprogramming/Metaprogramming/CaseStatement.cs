using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class CaseStatement : Statement, ILexicalScope<SwitchBuilder, Action>
    {

        private readonly SwitchBuilder builder;
        private readonly IEnumerable<Expression> testValues;

        internal CaseStatement(SwitchBuilder builder, IEnumerable<Expression> testValues)
        {
            this.builder = builder;
            this.testValues = testValues;
        }

        public SwitchBuilder Build(Action body)
        {
            body();
            return builder.Case(testValues, Build());
        }
    }
}
