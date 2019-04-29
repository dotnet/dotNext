using System;

namespace DotNext.Metaprogramming
{
    using ConditionalBuilder = Linq.Expressions.ConditionalBuilder;

    internal readonly struct BranchStatement : IStatement<ConditionalBuilder, Action>
    {
        private readonly ConditionalBuilder builder;
        private readonly bool branchType;

        internal BranchStatement(ConditionalBuilder builder, bool branchType)
        {
            this.builder = builder;
            this.branchType = branchType;
        }

        ConditionalBuilder IStatement<ConditionalBuilder, Action>.Build(Action scope, ILexicalScope body)
        {
            scope();
            return branchType ? builder.Then(body.Build()) : builder.Else(body.Build());
        }
    }
}