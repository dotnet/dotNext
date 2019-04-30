using System;

namespace DotNext.Metaprogramming
{
    using ConditionalBuilder = Linq.Expressions.ConditionalBuilder;

    internal sealed class BranchStatement : LexicalScope, ILexicalScope<ConditionalBuilder, Action>
    {
        private readonly ConditionalBuilder builder;
        private readonly bool branchType;

        internal BranchStatement(ConditionalBuilder builder, bool branchType, LexicalScope parent)
            : base(parent)
        {
            this.builder = builder;
            this.branchType = branchType;
        }

        ConditionalBuilder ILexicalScope<ConditionalBuilder, Action>.Build(Action scope)
        {
            scope();
            return branchType ? builder.Then(Build()) : builder.Else(Build());
        }
    }
}