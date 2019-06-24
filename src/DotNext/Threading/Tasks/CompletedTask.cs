using System.Threading.Tasks;

namespace DotNext.Threading.Tasks
{
    using Generic;

    /// <summary>
    /// Represents cache of completed tasks.
    /// </summary>
    /// <typeparam name="T">Type of the task result.</typeparam>
    /// <typeparam name="C">The constant value to be assigned to the completed task.</typeparam>
    public static class CompletedTask<T, C>
        where C : Constant<T>, new()
    {
        private static readonly T value = new C();

        /// <summary>
        /// Represents the completed task containing a value passed as constant through <typeparamref name="C"/> generic parameter.
        /// </summary>
        public static readonly Task<T> Task = System.Threading.Tasks.Task.FromResult<T>(value);

        internal static T WhenFaulted(Task<T> task) => task.IsFaulted ? value : task.Result;

        internal static T WhenCanceled(Task<T> task) => task.IsCanceled ? value : task.Result;

        internal static T WhenFaultedOrCanceled(Task<T> task) => task.IsFaulted | task.IsCanceled ? value : task.Result;
    }
}
