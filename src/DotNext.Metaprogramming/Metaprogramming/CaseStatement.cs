using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using SwitchBuilder = Linq.Expressions.SwitchBuilder;

    internal readonly struct CaseStatement : IStatement<SwitchBuilder, Action>
    {
        private readonly SwitchBuilder builder;
        private readonly IEnumerable<Expression> testValues;

        internal CaseStatement(SwitchBuilder builder, IEnumerable<Expression> testValues)
        {
            this.builder = builder;
            this.testValues = testValues;
        }

        internal CaseStatement(SwitchBuilder builder, Expression testValue)
            : this(builder, Sequence.Singleton(testValue))
        {
        }

        SwitchBuilder IStatement<SwitchBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return builder.Case(testValues, body.Build());
        }
    }
}
