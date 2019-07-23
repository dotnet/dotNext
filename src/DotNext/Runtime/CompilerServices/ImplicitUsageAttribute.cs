using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Marker interface indicating that program element is implicitly used from Reflection or pure IL code.
    /// </summary>
    [Conditional("DEBUG")]
    [SuppressMessage("Style", "CA1051", Justification = "This type for internal purposes only")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor| AttributeTargets.Event | AttributeTargets.Property, AllowMultiple = true, Inherited = false)]
    public sealed class ImplicitUsageAttribute : Attribute
    {
        /// <summary>
        /// Gets consumer method of the marked program element.
        /// </summary>
        public readonly string ConsumerMethod;

        /// <summary>
        /// Gets consumer of the marked program element.
        /// </summary>
        public readonly Type ConsumerType;

        /// <summary>
        /// Initializes a new attribute.
        /// </summary>
        /// <param name="consumerType">The type which contains code that uses the marked program element through reflection or pure IL code.</param>
        /// <param name="consumerMethod">The method declared in <paramref name="consumerType"/> containing Reflection or IL code.</param>
        public ImplicitUsageAttribute(Type consumerType, string consumerMethod = "")
        {
            ConsumerType = consumerType;
            ConsumerMethod = consumerMethod;
        }
    }
}