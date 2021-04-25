using System;

namespace DotNext.Metaprogramming
{
    internal sealed class BranchStatement : Statement, ILexicalScope<ConditionalBuilder, Action>
    {
        private readonly ConditionalBuilder builder;
        private readonly bool branchType;

        private BranchStatement(ConditionalBuilder builder, bool branchType)
        {
            this.builder = builder;
            this.branchType = branchType;
        }

        internal static BranchStatement Positive(ConditionalBuilder builder) => new (builder, true);

        internal static BranchStatement Negative(ConditionalBuilder builder) => new (builder, false);

        public ConditionalBuilder Build(Action body)
        {
            body();
            return branchType ? builder.Then(Build()) : builder.Else(Build());
        }
    }
}