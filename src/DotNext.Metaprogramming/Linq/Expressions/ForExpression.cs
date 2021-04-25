using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    using Seq = Collections.Generic.Sequence;

    /// <summary>
    /// Represents <c>for</c> loop as expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/for">for Statement</seealso>
    public sealed class ForExpression : CustomExpression, ILoopLabels
    {
        internal interface IBuilder : ILoopLabels
        {
            Expression MakeCondition(ParameterExpression loopVar);

            Expression MakeIteration(ParameterExpression loopVar);

            Expression MakeBody(ParameterExpression loopVar);
        }

        /// <summary>
        /// Represents expression builder.
        /// </summary>
        /// <seealso cref="Builder(Expression)"/>
        public sealed class LoopBuilder : IBuilder, IExpressionBuilder<ForExpression>
        {
            /// <summary>
            /// Represents constructor of loop condition.
            /// </summary>
            /// <param name="loopVar">The loop variable.</param>
            /// <returns>The condition of loop continuation. Must be of type <see cref="bool"/>.</returns>
            public delegate Expression Condition(ParameterExpression loopVar);

            /// <summary>
            /// Represents constructor of loop iteration.
            /// </summary>
            /// <param name="loopVar">The loop variable.</param>
            /// <returns>The loop iteration.</returns>
            public delegate Expression Iteration(ParameterExpression loopVar);

            /// <summary>
            /// Represents constructor of loop body.
            /// </summary>
            /// <param name="loopVar">The loop variable.</param>
            /// <param name="continueLabel">A label that can be used to produce <see cref="Expression.Continue(LabelTarget)"/> expression.</param>
            /// <param name="breakLabel">A label that can be used to produce <see cref="Expression.Break(LabelTarget)"/> expression.</param>
            /// <returns>The loop body.</returns>
            public delegate Expression Statement(ParameterExpression loopVar, LabelTarget continueLabel, LabelTarget breakLabel);

            private readonly LabelTarget continueLabel, breakLabel;
            private readonly Expression initialization;
            private Iteration? iteration;
            private Condition? condition;
            private Statement? body;

            internal LoopBuilder(Expression initialization)
            {
                this.initialization = initialization;
                breakLabel = Label(typeof(void), "break");
                continueLabel = Label(typeof(void), "continueLabel");
            }

            /// <summary>
            /// Defines loop condition.
            /// </summary>
            /// <param name="condition">A delegate used to construct condition.</param>
            /// <returns><c>this</c> builder.</returns>
            /// <seealso cref="Condition"/>
            public LoopBuilder While(Condition condition)
            {
                this.condition = condition;
                return this;
            }

            /// <summary>
            /// Defines loop body.
            /// </summary>
            /// <param name="body">A delegate used to construct loop body.</param>
            /// <returns><c>this</c> builder.</returns>
            /// <seealso cref="Statement"/>
            public LoopBuilder Do(Statement body)
            {
                this.body = body;
                return this;
            }

            /// <summary>
            /// Constructs loop iteration statement.
            /// </summary>
            /// <param name="iteration">A delegate used to construct iteration statement.</param>
            /// <returns><c>this</c> builder.</returns>
            /// <see cref="Iteration"/>
            public LoopBuilder Iterate(Iteration iteration)
            {
                this.iteration = iteration;
                return this;
            }

            /// <inheritdoc />
            LabelTarget ILoopLabels.BreakLabel => breakLabel;

            /// <inheritdoc />
            LabelTarget ILoopLabels.ContinueLabel => continueLabel;

            /// <inheritdoc />
            Expression IBuilder.MakeCondition(ParameterExpression loopVar) => condition is null ? Constant(true) : condition(loopVar);

            /// <inheritdoc />
            Expression IBuilder.MakeIteration(ParameterExpression loopVar) => iteration is null ? Empty() : iteration(loopVar);

            /// <inheritdoc />
            Expression IBuilder.MakeBody(ParameterExpression loopVar) => body is null ? Empty() : body(loopVar, continueLabel, breakLabel);

            /// <summary>
            /// Constructs a new instance of <see cref="ForExpression"/>.
            /// </summary>
            /// <returns>The constructed instance of <see cref="ForExpression"/>.</returns>
            public ForExpression Build() => new (initialization, this);
        }

        private Expression? body;

        internal ForExpression(Expression initialization, LabelTarget continueLabel, LabelTarget breakLabel, LoopBuilder.Condition condition)
        {
            Initialization = initialization;
            LoopVar = Variable(initialization.Type, "loop_var");
            Test = condition(LoopVar);
            if (Test.Type != typeof(bool))
                throw new ArgumentException(ExceptionMessages.TypeExpected<bool>(), nameof(condition));
            ContinueLabel = continueLabel ?? Label(typeof(void), "continue");
            BreakLabel = breakLabel ?? Label(typeof(void), "break");
        }

        internal ForExpression(Expression initialization, IBuilder builder)
            : this(initialization, builder.ContinueLabel, builder.BreakLabel, builder.MakeCondition)
        {
            body = builder.MakeBody(LoopVar).AddPrologue(false, Continue(ContinueLabel), builder.MakeIteration(LoopVar));
        }

        /// <summary>
        /// Creates a builder of <see cref="ForExpression"/>.
        /// </summary>
        /// <param name="initialization">Loop variable initialization expression.</param>
        /// <returns>A new instance of builder.</returns>
        public static LoopBuilder Builder(Expression initialization) => new (initialization);

        /// <summary>
        /// Represents condition of the loop continuation.
        /// </summary>
        public Expression Test { get; }

        /// <summary>
        /// Represents loop variable initialization expression.
        /// </summary>
        public Expression Initialization { get; }

        /// <summary>
        /// Represents loop variable initialized by <see cref="Initialization"/>.
        /// </summary>
        public ParameterExpression LoopVar { get; }

        /// <summary>
        /// Gets label that is used by the loop body as a break statement target.
        /// </summary>
        public LabelTarget BreakLabel { get; }

        /// <summary>
        /// Gets label that is used by the loop body as a continue statement target.
        /// </summary>
        public LabelTarget ContinueLabel { get; }

        /// <summary>
        /// Gets body of this loop.
        /// </summary>
        public Expression Body
        {
            get => body ?? Empty();
            internal set => body = value;
        }

        /// <summary>
        /// Always returns <see cref="void"/>.
        /// </summary>
        public override Type Type => typeof(void);

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            Expression body = Condition(Test, Body, Goto(BreakLabel), typeof(void));
            body = Loop(body, BreakLabel);
            return Block(typeof(void), Seq.Singleton(LoopVar), Assign(LoopVar, Initialization), body);
        }
    }
}