using System;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    internal static class DefaultContinuation<T>
    {
        internal static readonly Func<Task, T> Value = task => default(T);
    }
}