using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Allows to control type initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public sealed class BeforeFieldInitAttribute : Attribute
    {
        /// <summary>
        /// Creates a new attribute.
        /// </summary>
        /// <param name="lazy">If <see langword="true"/> then the type's initializer method is executed at, or sometime before, first access to any static field defined for that type.</param>
        public BeforeFieldInitAttribute(bool lazy) => LazyTypeInit = lazy;

        /// <summary>
        /// Gets type initialization policy.
        /// </summary>
        public readonly bool LazyTypeInit;
    }
}
