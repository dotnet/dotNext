using System;

namespace DotNext.Runtime.ExceptionServices
{
    internal static class ExceptionHelpers
    {
        internal static Exception GetFirstException(this AggregateException e)
            => e.InnerException ?? e;
    }
}