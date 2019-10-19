using System;
using Xunit;

namespace DotNext
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class Require64BitProcessAttribute : FactAttribute
    {
        public Require64BitProcessAttribute()
        {
            if (!Environment.Is64BitProcess)
                Skip = "Test requires 64-bit process";
        }
    }
}
