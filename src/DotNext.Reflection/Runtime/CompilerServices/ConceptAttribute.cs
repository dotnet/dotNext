using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that attributed class is a concept type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
    public sealed class ConceptAttribute : Attribute
    {
    }
}