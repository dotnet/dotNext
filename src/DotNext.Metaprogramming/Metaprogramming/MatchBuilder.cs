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
        private delegate ConditionalExpression PatternMatch(LabelTarget endOfMatch);

        private interface ICaseStatementBuilder
        {
            Expression Build(ParameterExpression value);
        }

        internal abstract class MatchStatement : Statement, ILexicalScope<MatchBuilder, Action<ParameterExpression>>
        {
            private protected readonly struct CaseStatementBuilder : ICaseStatementBuilder
            {
                private readonly Action<ParameterExpression> scope;
                private readonly MatchStatement statement;

                internal CaseStatementBuilder(Action<ParameterExpression> scope, MatchStatement statement)
                {
                    this.scope = scope;
                    this.statement = statement;
                }

                Expression ICaseStatementBuilder.Build(ParameterExpression value)
                {
                    scope(value);
                    return statement.Build();
                }
            }

            private readonly MatchBuilder builder;

            private protected MatchStatement(MatchBuilder builder) => this.builder = builder;

            private protected abstract MatchBuilder Build(MatchBuilder builder, CaseStatementBuilder caseStatement);

            public MatchBuilder Build(Action<ParameterExpression> body) => Build(builder, new CaseStatementBuilder(body, this));
        }

        internal sealed class DefaultStatement : Statement, ILexicalScope<MatchBuilder, Action>
        {
            private readonly MatchBuilder builder;

            internal DefaultStatement(MatchBuilder builder) => this.builder = builder;

            public MatchBuilder Build(Action scope)
            {
                scope();
                return builder.Default(Build());
            }
        }

        private sealed class MatchByConditionStatement : MatchStatement
        {
            private readonly Pattern pattern;

            internal MatchByConditionStatement(MatchBuilder builder, Pattern pattern) : base(builder) => this.pattern = pattern;

            private protected override MatchBuilder Build(MatchBuilder builder, CaseStatementBuilder caseStatement)
                => builder.MatchByCondition(pattern, caseStatement);
        }

        private class MatchByTypeStatement : MatchStatement
        {
            private protected readonly Type expectedType;

            internal MatchByTypeStatement(MatchBuilder builder, Type expectedType): base(builder) => this.expectedType = expectedType;

            private protected override MatchBuilder Build(MatchBuilder builder, CaseStatementBuilder caseStatement)
                => builder.MatchByType(expectedType, caseStatement);
        }

        private sealed class MatchByTypeWithConditionStatement : MatchByTypeStatement
        {
            private readonly Pattern condition;

            internal MatchByTypeWithConditionStatement(MatchBuilder builder, Type expectedType, Pattern condition) : base(builder, expectedType) => this.condition = condition;

            private protected override MatchBuilder Build(MatchBuilder builder, CaseStatementBuilder caseStatement)
                => builder.MatchByType(expectedType, condition, caseStatement);
        }

        /// <summary>
        /// Represents condition constructor for the switch case.
        /// </summary>
        /// <param name="value">The value participating in pattern match.</param>
        /// <returns>The condition for evaluation.</returns>
        public delegate Expression Pattern(ParameterExpression value);

        /// <summary>
        /// Represents constructor of the action to be executed if <paramref name="value"/>
        /// matches to the pattern defined by <see cref="Pattern"/>.
        /// </summary>
        /// <param name="value">The value participating in pattern match.</param>
        /// <returns>The action to be executed if object matches to the pattern.</returns>
        public delegate Expression CaseStatement(ParameterExpression value);

        

        private readonly struct CaseStatementBuilder : ICaseStatementBuilder
        {
            private readonly CaseStatement statement;

            private CaseStatementBuilder(CaseStatement statement) => this.statement = statement;

            Expression ICaseStatementBuilder.Build(ParameterExpression value) => statement(value);

            public static implicit operator CaseStatementBuilder(CaseStatement statement) => new CaseStatementBuilder(statement);
        }

        private readonly ParameterExpression value;
        private readonly BinaryExpression assignment;
        private readonly ICollection<PatternMatch> patterns;
        private Expression defaultCase;

        internal MatchBuilder(Expression value, ILexicalScope currentScope)
            : base(currentScope)
        {
            patterns = new LinkedList<PatternMatch>();
            if(value is ParameterExpression param)
                this.value = param;
            else
            {
                this.value = Expression.Variable(value.Type);
                assignment = Expression.Assign(this.value, value);
            }
        }

        private static PatternMatch MatchByCondition<B>(ParameterExpression value, Pattern condition, B builder)
            where B : struct, ICaseStatementBuilder
        {
            var test = condition(value);
            var body = builder.Build(value);
            return endOfMatch => Expression.IfThen(test, endOfMatch.Goto(body));
        }

        private MatchBuilder MatchByCondition<B>(Pattern condition, B builder)
            where B : struct, ICaseStatementBuilder
        {
            patterns.Add(MatchByCondition(value, condition, builder));
            return this;
        }

        private static PatternMatch MatchByType<B>(ParameterExpression value, Type expectedType, B builder)
            where B : struct, ICaseStatementBuilder
        {
            var test = value.InstanceOf(expectedType);
            var typedValue = Expression.Variable(expectedType);
            var body = builder.Build(typedValue);
            return endOfMatch => Expression.IfThen(test, Expression.Block(Sequence.Singleton(typedValue), typedValue.Assign(value.Convert(expectedType)), endOfMatch.Goto(body)));
        }

        private MatchBuilder MatchByType<B>(Type expectedType, B builder)
            where B : struct, ICaseStatementBuilder
        {
            patterns.Add(MatchByType(value, expectedType, builder));
            return this;
        }

        private static PatternMatch MatchByType<B>(ParameterExpression value, Type expectedType, Pattern condition, B builder)
            where B : struct, ICaseStatementBuilder
        {
            var test = value.InstanceOf(expectedType);
            var typedVar = Expression.Variable(expectedType);
            var typedVarInit = typedVar.Assign(value.Convert(expectedType));
            var body = builder.Build(typedVar);
            var test2 = condition(typedVar);
            return endOfMatch => Expression.IfThen(test, Expression.Block(Sequence.Singleton(typedVar), typedVarInit, Expression.IfThen(test2, endOfMatch.Goto(body))));
        }

        private MatchBuilder MatchByType<B>(Type expectedType, Pattern condition, B builder)
            where B : struct, ICaseStatementBuilder
        {
            patterns.Add(MatchByType(value, expectedType, builder));
            return this;
        }

        /// <summary>
        /// Defines pattern matching.
        /// </summary>
        /// <param name="pattern">The condition representing pattern.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(Pattern pattern, CaseStatement body)
            => MatchByCondition<CaseStatementBuilder>(pattern, body);

        internal MatchStatement Case(Pattern pattern) => new MatchByConditionStatement(this, pattern);

        internal MatchStatement Case(IEnumerable<(string, Expression)> structPattern) => Case(StructuralPattern(structPattern));

        internal MatchStatement Case(object structPattern) => Case(GetProperties(structPattern));

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
        public MatchBuilder Case(Type targetType, Pattern pattern, CaseStatement body)
            => MatchByType<CaseStatementBuilder>(targetType, pattern, body);
        
        internal MatchStatement Case(Type expectedType, Pattern pattern) => new MatchByTypeWithConditionStatement(this, expectedType, pattern); 

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <remarks>
        /// This method equivalent to <c>case T value: body();</c>
        /// </remarks>
        /// <param name="targetType">The expected type of the value.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case(Type targetType, CaseStatement body)
            => MatchByType<CaseStatementBuilder>(targetType, body);

        internal MatchStatement Case(Type expectedType) => new MatchByTypeStatement(this, expectedType);

        /// <summary>
        /// Defines pattern matching based on the expected type of value.
        /// </summary>
        /// <typeparam name="T">The expected type of the value.</typeparam>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case<T>(CaseStatement body)
            => Case(typeof(T), body);
        
        private static Pattern StructuralPattern(IEnumerable<(string FieldOrProperty, Expression Value)> structPattern)
            => delegate(ParameterExpression obj)
            {
                var result = default(Expression);
                foreach (var (name, value) in structPattern)
                {
                    var element = Expression.PropertyOrField(obj, name).Equal(value);
                    result = result is null ? element : result.AndAlso(element);
                }
                return result;
            };

        /// <summary>
        /// Defines pattern matching based on structural matching.
        /// </summary>
        /// <param name="structPattern">A sequence of fields or properties with their expected values.</param>
        /// <param name="body">The action to be executed if object matches to the pattern.</param>
        /// <returns><c>this</c> builder.</returns>
        public MatchBuilder Case((string FieldOrProperty, Expression Value)[] structPattern, CaseStatement body)
            => Case(StructuralPattern(structPattern), body);

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
        public MatchBuilder Case(object structPattern, CaseStatement body)
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
                instructions.Add(pattern(endOfMatch));
            //handle default
            if(!(defaultCase is null))
                instructions.Add(Expression.Goto(endOfMatch, defaultCase));
            //setup label as last instruction
            instructions.Add(Expression.Label(endOfMatch, Expression.Default(endOfMatch.Type)));
            return assignment is null ? Expression.Block(instructions) : Expression.Block(Sequence.Singleton(value), instructions);
        }

        private protected override void Cleanup()
        {
            patterns.Clear();
            defaultCase = null;
            base.Cleanup();
        }
    }
}