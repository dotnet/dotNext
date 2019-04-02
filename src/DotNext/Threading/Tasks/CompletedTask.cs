using System;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Generic;

    public static class CompletedTask<T, C>
        where C: Constant<T>, new()
    {
        public static readonly Task<T> Task = System.Threading.Tasks.Task.FromResult(Constant<T>.Of<C>(false));
    }
}
