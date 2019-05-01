using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DotNext.Linq.Expressions
{
    internal sealed class Assertion : Expression
    {
        internal Assertion(Expression condition, string message)
        {
            if(condition is null)
                throw new ArgumentNullException(nameof(condition));
            else if(condition.Type != typeof(bool))
                throw new ArgumentException(ExceptionMessages.BoolExpressionExpected, nameof(condition));
            Message = message;
            Condition = condition;
        }

        public string Message { get; }

        public new Expression Condition { get; }

        public override Expression Reduce() 
            => string.IsNullOrEmpty(Message) ? typeof(Debug).CallStatic(nameof(Debug.Assert), Condition) : typeof(Debug).CallStatic(nameof(Debug.Assert), Condition, Constant(Message));
    }
}