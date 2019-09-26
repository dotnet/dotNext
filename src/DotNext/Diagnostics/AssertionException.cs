using System;

namespace DotNext.Diagnostics
{
    /// <summary>
    /// Represents failed runtime assertion.
    /// </summary>
    public sealed class AssertionException : Exception
    {
        internal AssertionException(string message)
            : base(message)
        {
        }       
    }
}