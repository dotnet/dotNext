using System;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that documentation generation tool should not generate documentation
    /// for the marked program element.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
    public sealed class SuppressDocumentationAttribute : Attribute
    {
    }
}