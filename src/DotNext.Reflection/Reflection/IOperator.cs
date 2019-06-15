using System;
using System.Linq.Expressions;

namespace DotNext.Reflection
{
    /// <summary>
    /// Represents operator.
    /// </summary>
    /// <typeparam name="D">Type of delegate describing signature of operator.</typeparam>
    public interface IOperator<out D>
        where D : Delegate
    {
        /// <summary>
        /// Gets type of operator.
        /// </summary>
        ExpressionType Type { get; }

        /// <summary>
        /// Gets delegate representing operator.
        /// </summary>
        D Invoker { get; }
    }
}