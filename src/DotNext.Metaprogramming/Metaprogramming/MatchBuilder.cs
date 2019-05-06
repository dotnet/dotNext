using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    /// <summary>
    /// Represents pattern matcher.
    /// </summary>
    public sealed class MatchBuilder : ExpressionBuilder<BlockExpression>
    {
        internal sealed class DefaultStatement : Statement, ILexicalScope<MatchBuilder, System.Action>
        {
            private readonly MatchBuilder builder;

            internal DefaultStatement(MatchBuilder builder) => this.builder = builder;

            public MatchBuilder Build(System.Action scope)
            {
                scope();
                return builder.Default(Build());
            }
        }

        internal sealed class MatchByConditionStatement : Statement, ILexicalScope<MatchBuilder, Action<ParameterExpression>>
        {
            private readonly Pattern pattern;
            private readonly MatchBuilder builder;

            internal MatchByConditionStatement(MatchBuilder builder, Pattern pattern)
            {
                this.builder = builder;
                this.pattern = pattern;
            }

            public MatchBuilder Build(Action<ParameterExpression> body)
            {
                body(builder.value);
                builder.patterns.Add(new MatchByCondition(builder.value, pattern, Build));
                return builder;
            }
        }

        internal sealed class MatchByTypeStatement : Statement, ILexicalScope<MatchBuilder, Action<ParameterExpression>>
        {
            private readonly MatchBuilder builder;
            private readonly Type expectedType;

            internal MatchByTypeStatement(MatchBuilder builder, Type expectedType)
            {
                this.builder = builder;
                this.expectedType = expectedType;
            }

            public MatchBuilder Build(Action<ParameterExpression> body)
            {
                builder.patterns.Add(new MatchByType(builder.value, expectedType, body, Build));
                return builder;
            }
        }

        /// <summary>
        /// Represents condition constructor for the switch case.
        /// </summary>
        /// <param name="variable">The value participating in pattern match.</param>
        /// <returns>The condition for evaluation.</returns>
        public delegate Expression Pattern(ParameterExpression variable);

        /// <summary>
        /// Represents constructor of the action to be executed if <paramref name="variable"/>
        /// matches to the pattern defined by <see cref="Pattern"/>.
        /// </summary>
        /// <param name="variable">The value participating in pattern match.</param>
        /// <returns>The action to be executed if object matches to the pattern.</returns>
        public delegate Expression Action(ParameterExpression variable);
        
        private interface IPatternMatch
        {
            ConditionalExpression CreateExpression(LabelTarget endOfMatch);
        }

        private sealed class MatchByCondition : IPatternMatch
        {
            private readonly Expression test;
            private readonly Expression body;

            internal MatchByCondition(ParameterExpression value, Pattern condition, Action body)
            {
                test = condition(value);
                this.body = body(value);
            }

            internal MatchByCondition(ParameterExpression value, Pattern condition, Func<Expression> body)
            {
                test = condition(value);
                this.body = body();
            }

            public ConditionalExpression CreateExpression(LabelTarget endOfMatch)
                => Expression.IfThen(test, endOfMatch.Goto(body));
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

            internal MatchByType(ParameterExpression value, Type expectedType, Action<ParameterExpression> body, Func<Expression> builder)
            {
                test = value.InstanceOf(expectedType);
                var typedValue = Expression.Variable(expectedType);
                var assignment = typedValue.Assign(value.Convert(expectedType));
                body(typedValue);
                this.body = Expression.Block(Sequence.Singleton(typedValue), assignment, builder());
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

            internal MatchByTypeWithCondition(ParameterExpression value, Type expectedType, Pattern condition, Action body)
            {
                test = value.InstanceOf(expectedType);
                typedVar = Expression.Variable(expectedType);
                typedVarInit = typedVar.Assign(value.Convert(expectedType));
                typedTest = Expression.IfThen(condition(typedVar), body(typedVar));
            }

            internal MatchByTypeWithCondition(ParameterExpression value, Type expectedType, Pattern condition, Expression body)
            {

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

        /// <summary>
        /// Defines pattern matching.
        /// </summary>
        /// <param name="pattern">The condition representing pattern.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(Pattern pattern, Action body)
        {
            patterns.Add(new MatchByCondition(value, pattern, body));
            return this;
        }

        internal MatchByConditionStatement Case(Pattern pattern)
            => new MatchByConditionStatement(this, pattern);

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <remarks>
        /// This method equivalent to <c>case T value where condition(value): body();</c>
        /// </remarks>
        /// <param name="targetType">The expected type of the value.</param>
        /// <param name="pattern">Additional condition associated with the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(Type targetType, Pattern pattern, Action body)
        {
            patterns.Add(new MatchByTypeWithCondition(value, targetType, pattern, body));
            return this;
        }

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <remarks>
        /// This method equivalent to <c>case T value: body();</c>
        /// </remarks>
        /// <param name="targetType">The expected type of the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(Type targetType, Action body)
        {
            patterns.Add(new MatchByType(value, targetType, body));
            return this;
        }

        internal MatchByTypeStatement Case(Type expectedType) => new MatchByTypeStatement(this, expectedType);

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case<T>(Action body)
            => Case(typeof(T), body);

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="structPattern">A sequence of fields or properties with their expected values.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(IEnumerable<(string FieldOrProperty, Expression Value)> structPattern, Action body)
        {
            Expression FieldOrProperty(ParameterExpression obj)
            {
                var result = default(Expression);
                foreach (var (name, value) in structPattern)
                {
                    var element = Expression.PropertyOrField(obj, name).Equal(value);
                    result = result is null ? element : result.AndAlso(element);
                }
                return result;
            }
            return Case(FieldOrProperty, body);
        }

        private static IEnumerable<(string FieldOrProperty, Expression Value)> GetProperties(object structPattern)
        {
            foreach (var property in structPattern.GetType().GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance))
            {
                var value = property.GetValue(structPattern);
                if (value is null)
                    yield return (property.Name, Expression.Default(property.PropertyType));
                else if (value is Expression expr)
                    yield return (property.Name, expr);
                else
                    yield return (property.Name, Expression.Constant(value, property.PropertyType));
            }
        }

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="structPattern">The structure pattern represented by instance of anonymous type.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(object structPattern, Action body)
            => Case(GetProperties(structPattern), body);

        /// <summary>
        /// Defines default behavior in case when all defined patterns are false positive.
        /// </summary>
        /// <param name="expression">The expression to be evaluated as default case.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Default(Expression expression)
        {
            defaultCase = expression;
            return this;
        }

        internal DefaultStatement Default() => new DefaultStatement(this);

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