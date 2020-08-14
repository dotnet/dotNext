using System;
using Xunit;

namespace DotNext
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class OSDependentFactAttribute : FactAttribute
    {
        public OSDependentFactAttribute(PlatformID target)
        {
            if (Environment.OSVersion.Platform != target)
                Skip = "Not supported by host operating system";
        }
    }
}
