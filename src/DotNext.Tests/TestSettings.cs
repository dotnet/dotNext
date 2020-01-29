using System;
using System.Diagnostics.CodeAnalysis;

namespace DotNext
{
    [ExcludeFromCodeCoverage]
    internal static class TestSettings
    {
        internal const int TimeoutMillis = 20_000;
        internal static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(TimeoutMillis);
    }
}