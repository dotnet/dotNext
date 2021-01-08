using System;
using System.Threading;

namespace DotNext.Threading
{
#if !NETSTANDARD2_1
    /// <summary>
    /// Represents factory of thread pool work items.
    /// </summary>
    [CLSCompliant(false)]
    public static unsafe class ThreadPoolWorkItemFactory
    {
        private sealed class ThreadPoolWorkItem<T> : Tuple<T>, IThreadPoolWorkItem
        {
            private readonly delegate*<T, void> invoker;

            internal ThreadPoolWorkItem(delegate*<T, void> invoker, T arg)
                : base(arg)
                => this.invoker = invoker;

            void IThreadPoolWorkItem.Execute() => invoker(Item1);
        }

        private sealed class ThreadPoolWorkItem<T1, T2> : Tuple<T1, T2>, IThreadPoolWorkItem
        {
            private readonly delegate*<T1, T2, void> invoker;

            internal ThreadPoolWorkItem(delegate*<T1, T2, void> invoker, T1 arg1, T2 arg2)
                : base(arg1, arg2)
                => this.invoker = invoker;

            void IThreadPoolWorkItem.Execute() => invoker(Item1, Item2);
        }

        private sealed class ThreadPoolWorkItem<T1, T2, T3> : Tuple<T1, T2, T3>, IThreadPoolWorkItem
        {
            private readonly delegate*<T1, T2, T3, void> invoker;

            internal ThreadPoolWorkItem(delegate*<T1, T2, T3, void> invoker, T1 arg1, T2 arg2, T3 arg3)
                : base(arg1, arg2, arg3)
                => this.invoker = invoker;

            void IThreadPoolWorkItem.Execute() => invoker(Item1, Item2, Item3);
        }

        /// <summary>
        /// Creates thread pool work item.
        /// </summary>
        /// <param name="workItem">The pointer to the method implementing work item logic.</param>
        /// <param name="arg">The argument to be passed to the method implementing work item logic.</param>
        /// <typeparam name="T">The type of the work item argument.</typeparam>
        /// <returns>The work item.</returns>
        public static IThreadPoolWorkItem Create<T>(delegate*<T, void> workItem, T arg)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            return new ThreadPoolWorkItem<T>(workItem, arg);
        }

        /// <summary>
        /// Creates thread pool work item.
        /// </summary>
        /// <param name="workItem">The pointer to the method implementing work item logic.</param>
        /// <param name="arg1">The first argument to be passed to the method implementing work item logic.</param>
        /// <param name="arg2">The second argument to be passed to the method implementing work item logic.</param>
        /// <typeparam name="T1">The type of the work item first argument.</typeparam>
        /// <typeparam name="T2">The type of the work item second argument.</typeparam>
        /// <returns>The work item.</returns>
        public static IThreadPoolWorkItem Create<T1, T2>(delegate*<T1, T2, void> workItem, T1 arg1, T2 arg2)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            return new ThreadPoolWorkItem<T1, T2>(workItem, arg1, arg2);
        }

        /// <summary>
        /// Creates thread pool work item.
        /// </summary>
        /// <param name="workItem">The pointer to the method implementing work item logic.</param>
        /// <param name="arg1">The first argument to be passed to the method implementing work item logic.</param>
        /// <param name="arg2">The second argument to be passed to the method implementing work item logic.</param>
        /// <typeparam name="T1">The type of the work item first argument.</typeparam>
        /// <typeparam name="T2">The type of the work item second argument.</typeparam>
        /// <typeparam name="T3">The type of the work item third argument.</typeparam>
        /// <returns>The work item.</returns>
        public static IThreadPoolWorkItem Create<T1, T2, T3>(delegate*<T1, T2, T3, void> workItem, T1 arg1, T2 arg2, T3 arg3)
        {
            if (workItem == null)
                throw new ArgumentNullException(nameof(workItem));

            return new ThreadPoolWorkItem<T1, T2, T3>(workItem, arg1, arg2, arg3);
        }
    }
#endif
}