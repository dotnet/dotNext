using System;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    internal sealed class BranchStatement : Statement<ConditionalBuilder>
    {
        internal readonly struct Factory : IFactory<BranchStatement>
        {
            private readonly ConditionalBuilder builder;
            private readonly bool branchType;

            internal Factory(ConditionalBuilder builder, bool branchType)
            {
                this.builder = builder;
                this.branchType = branchType;
            }

            public BranchStatement Create(LexicalScope parent) => new BranchStatement(builder, branchType, parent);
        }

        private readonly ConditionalBuilder builder;
        private readonly bool branchType;

        private BranchStatement(ConditionalBuilder builder, bool branchType, LexicalScope parent)
            : base(parent)
        {
            this.builder = builder;
            this.branchType = branchType;
        }

        private protected override ConditionalBuilder CreateExpression(Expression body)
            => branchType ? builder.Then(body) : builder.Else(body);
    }
}