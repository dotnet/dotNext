using System;
using System.Diagnostics.CodeAnalysis;
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
        [NotNull]
        D Invoker { get; }
    }
}