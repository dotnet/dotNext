using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Generic;
    using static Runtime.Intrinsics;

    /// <summary>
    /// Represents cache of completed tasks.
    /// </summary>
    /// <typeparam name="T">Type of the task result.</typeparam>
    /// <typeparam name="TConstant">The constant value to be assigned to the completed task.</typeparam>
    public static class CompletedTask<T, TConstant>
        where TConstant : Constant<T>, new()
    {
        private static readonly T Value = new TConstant();

        /// <summary>
        /// Represents the completed task containing a value passed as constant through <typeparamref name="TConstant"/> generic parameter.
        /// </summary>
        public static readonly Task<T> Task = System.Threading.Tasks.Task.FromResult(Value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFaulted(Task<T> task, object? predicate)
            => task.IsFaulted && Cast<Predicate<AggregateException>>(predicate).Invoke(task.Exception!);

        internal static T WhenFaulted(Task<T> task, object? predicate)
            => IsFaulted(task, predicate) ? Value : task.GetAwaiter().GetResult();

        internal static T WhenCanceled(Task<T> task) => task.IsCanceled ? Value : task.GetAwaiter().GetResult();

        internal static T WhenFaultedOrCanceled(Task<T> task, object? predicate) => IsFaulted(task, predicate) || task.IsCanceled ? Value : task.GetAwaiter().GetResult();
    }
}
