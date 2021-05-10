using System;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents <c>while</c> loop expression.
    /// </summary>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/while">while Statement</seealso>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/do">do-while Statement</seealso>
    public sealed class WhileExpression : CustomExpression, ILoopLabels
    {
        /// <summary>
        /// Represents constructor of the loop body.
        /// </summary>
        /// <param name="continueLabel">A label that can be used to produce <see cref="Expression.Continue(LabelTarget)"/> expression.</param>
        /// <param name="breakLabel">A label that can be used to produce <see cref="Expression.Break(LabelTarget)"/> expression.</param>
        /// <returns>The loop body.</returns>
        public delegate Expression Statement(LabelTarget continueLabel, LabelTarget breakLabel);

        private readonly bool conditionFirst;
        private Expression? body;

        internal WhileExpression(Expression test, LabelTarget? continueLabel, LabelTarget? breakLabel, bool checkConditionFirst)
        {
            if (test is null)
                throw new ArgumentNullException(nameof(test));
            else if (test.Type != typeof(bool))
                throw new ArgumentException(ExceptionMessages.TypeExpected<bool>(), nameof(test));
            Test = test;
            conditionFirst = checkConditionFirst;

            ContinueLabel = continueLabel ?? Label(typeof(void), "continue");
            BreakLabel = breakLabel ?? Label(typeof(void), "break");
        }

        private WhileExpression(Expression test, bool checkConditionFirst)
            : this(test, null, null, checkConditionFirst)
        {
        }

        /// <summary>
        /// Creates a new loop expression.
        /// </summary>
        /// <param name="test">The loop condition.</param>
        /// <param name="body">The delegate that is used to construct loop body.</param>
        /// <param name="checkConditionFirst"><see langword="true"/> to check condition before loop body; <see langword="false"/> to use do-while style.</param>
        /// <returns>The constructed loop expression.</returns>
        public static WhileExpression Create(Expression test, Statement body, bool checkConditionFirst)
        {
            var result = new WhileExpression(test, checkConditionFirst);
            result.Body = body(result.ContinueLabel, result.BreakLabel);
            return result;
        }

        /// <summary>
        /// Creates a new loop expression.
        /// </summary>
        /// <param name="test">The loop condition.</param>
        /// <param name="body">The loop body.</param>
        /// <param name="checkConditionFirst"><see langword="true"/> to check condition before loop body; <see langword="false"/> to use do-while style.</param>
        /// <returns>The constructed loop expression.</returns>
        public static WhileExpression Create(Expression test, Expression body, bool checkConditionFirst)
            => new(test, checkConditionFirst) { Body = body };

        /// <summary>
        /// Gets label that is used by the loop body as a break statement target.
        /// </summary>
        public LabelTarget BreakLabel { get; }

        /// <summary>
        /// Gets label that is used by the loop body as a continue statement target.
        /// </summary>
        public LabelTarget ContinueLabel { get; }

        /// <summary>
        /// Gets loop condition.
        /// </summary>
        public Expression Test { get; }

        /// <summary>
        /// Gets body of the loop.
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
        /// Reconstructs loop expression with a new body.
        /// </summary>
        /// <param name="body">The body of the loop.</param>
        /// <returns>Updated loop expression.</returns>
        public WhileExpression Update(Expression body) => new(Test, conditionFirst) { Body = body };

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            Expression loopBody;
            if (conditionFirst)
            {
                loopBody = Condition(Test, Body, Goto(BreakLabel), typeof(void));
                return Loop(loopBody, BreakLabel, ContinueLabel);
            }
            else
            {
                var condition = Condition(Test, Empty(), Goto(BreakLabel), typeof(void));
                loopBody = Block(typeof(void), Body, Label(ContinueLabel), condition);
                return Loop(loopBody, BreakLabel);
            }
        }
    }
}