using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    /// <summary>
    /// Represents pattern matcher.
    /// </summary>
    public sealed class MatchBuilder : ExpressionBuilder<BlockExpression>
    {
        private readonly struct Case
        {
            private readonly Expression pattern;
            private readonly Expression value;

            internal Case(Expression pattern, Expression value)
            {
                this.pattern = pattern;
                this.value = value;
            }

            internal ConditionalExpression CreateExpression(Type resultType, LabelTarget endOfMatch)
                => Expression.IfThen(pattern, Expression.Goto(endOfMatch, value));
        }

        private readonly ParameterExpression value;
        private readonly BinaryExpression assignment;
        private readonly ICollection<Case> patterns;
        private Expression defaultCase;

        internal MatchBuilder(Expression value, ILexicalScope currentScope)
            : base(currentScope)
        {
            patterns = new LinkedList<Case>();
            if(value is ParameterExpression param)
                this.value = param;
            else
            {
                this.value = Expression.Variable(value.Type);
                assignment = Expression.Assign(this.value, value);
            }
        }

        public MatchBuilder When(Type targetType, Func<ParameterExpression, Expression> body)
        {
            var typedVar = Expression.Variable(targetType);
            var statement = Expression.Block(Sequence.Singleton(typedVar),
                typedVar.Assign(value.Convert(targetType)),
                body(typedVar));
            patterns.Add(new Case(value.InstanceOf(targetType), statement));
            return this;
        }

        public MatchBuilder When<T>(Func<ParameterExpression, Expression> body)
            => When(typeof(T), body);
        
        public MatchBuilder Default(Expression value)
        {
            defaultCase = value;
            return this;
        }

        private protected override BlockExpression Build()
        {
            var endOfMatch = Expression.Label(Type, "end");
            //handle patterns
            ICollection<Expression> instructions = new LinkedList<Expression>();
            if(!(assignment is null))
                instructions.Add(assignment);
            foreach(var pattern in patterns)
                instructions.Add(pattern.CreateExpression(Type, endOfMatch));
            //handle default
            if(!(defaultCase is null))
                instructions.Add(Expression.Goto(endOfMatch, defaultCase));
            //setup label as last instruction
            instructions.Add(Expression.Label(endOfMatch, Expression.Default(endOfMatch.Type)));
            return assignment is null ? Expression.Block(instructions) : Expression.Block(Sequence.Singleton(value), instructions);
        }
    }
}