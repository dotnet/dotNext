using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that the caller of the attributed method should be synchronized.
    /// </summary>
    /// <remarks>
    /// This attribute is for internal purposes only.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor, AllowMultiple = false)]
    [Conditional("DEBUG")]
    [CLSCompliant(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class CallerMustBeSynchronizedAttribute : Attribute
    {
    }
}