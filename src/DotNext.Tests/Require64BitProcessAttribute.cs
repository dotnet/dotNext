using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [ExcludeFromCodeCoverage]
    public sealed class Require64BitProcessAttribute : FactAttribute
    {
        public Require64BitProcessAttribute()
        {
            if (!Environment.Is64BitProcess)
                Skip = "Test requires 64-bit process";
        }
    }
}
