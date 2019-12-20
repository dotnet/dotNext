using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    [ExcludeFromCodeCoverage]
    public sealed class OSDependentFactAttribute : FactAttribute
    {
        public OSDependentFactAttribute(PlatformID target)
        {
            if (Environment.OSVersion.Platform != target)
                Skip = "Not supported by host operating system";
        }
    }
}
