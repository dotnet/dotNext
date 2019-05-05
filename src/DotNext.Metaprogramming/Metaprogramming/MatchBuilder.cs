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
        public delegate Expression Condition(ParameterExpression variable);
        public delegate Expression Action(ParameterExpression variable);
        
        private interface IPatternMatch
        {
            ConditionalExpression CreateExpression(LabelTarget endOfMatch);
        }

        private sealed class MatchByType : IPatternMatch
        {
            private readonly Expression test;
            private readonly Expression body;

            internal MatchByType(ParameterExpression value, Type expectedType, Action body)
            {
                test = value.InstanceOf(expectedType);
                var typedValue = Expression.Variable(expectedType);
                var assignment = typedValue.Assign(value.Convert(expectedType));
                this.body = Expression.Block(Sequence.Singleton(typedValue), assignment, body(typedValue));
            }

            public ConditionalExpression CreateExpression(LabelTarget endOfMatch)
                => Expression.IfThen(test, endOfMatch.Goto(body));
        }

        private sealed class MatchByTypeWithCondition : IPatternMatch
        {
            private readonly Expression test;
            private readonly ConditionalExpression typedTest;
            private readonly ParameterExpression typedVar;
            private readonly BinaryExpression typedVarInit;

            internal MatchByTypeWithCondition(ParameterExpression value, Type expectedType, Condition condition, Action body)
            {
                test = value.InstanceOf(expectedType);
                typedVar = Expression.Variable(expectedType);
                typedVarInit = typedVar.Assign(value.Convert(expectedType));
                typedTest = Expression.IfThen(condition(typedVar), body(typedVar));
            }

            public ConditionalExpression CreateExpression(LabelTarget endOfMatch)
            {
                var typedTest = Expression.IfThen(this.typedTest.Test, endOfMatch.Goto(this.typedTest.IfTrue));
                return Expression.IfThen(test, Expression.Block(Sequence.Singleton(typedVar), typedVarInit, typedTest));
            }
        }

        private readonly ParameterExpression value;
        private readonly BinaryExpression assignment;
        private readonly ICollection<IPatternMatch> patterns;
        private Expression defaultCase;

        internal MatchBuilder(Expression value, ILexicalScope currentScope)
            : base(currentScope)
        {
            patterns = new LinkedList<IPatternMatch>();
            if(value is ParameterExpression param)
                this.value = param;
            else
            {
                this.value = Expression.Variable(value.Type);
                assignment = Expression.Assign(this.value, value);
            }
        }

        public MatchBuilder Case(Type targetType, Condition condition, Action body)
        {
            patterns.Add(new MatchByTypeWithCondition(value, targetType, condition, body));
            return this;
        }

        public MatchBuilder Case(Type targetType, Action body)
        {
            patterns.Add(new MatchByType(value, targetType, body));
            return this;
        }

        public MatchBuilder Case<T>(Action body)
            => Case(typeof(T), body);
        
        public MatchBuilder Case(Type targetType, (string FieldOrProperty, Expression Value)[] structPattern, Action body)
        {
            return this;
        }

        public MatchBuilder Case<T>((string FieldOrProperty, Expression Value)[] structPattern, Action body)
            => Case(typeof(T), structPattern, body);

        public MatchBuilder Case<P>(Type targetType, P structPattern, Action body)
        {
            return this;
        }
        
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
                instructions.Add(pattern.CreateExpression(endOfMatch));
            //handle default
            if(!(defaultCase is null))
                instructions.Add(Expression.Goto(endOfMatch, defaultCase));
            //setup label as last instruction
            instructions.Add(Expression.Label(endOfMatch, Expression.Default(endOfMatch.Type)));
            return assignment is null ? Expression.Block(instructions) : Expression.Block(Sequence.Singleton(value), instructions);
        }
    }
}