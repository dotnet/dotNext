using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents lightweight version of <see cref="Task"/>.
    /// </summary>
    public abstract class Future
    {
        private protected Action continuation;

        private protected Future() { }

        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        public abstract bool IsCompleted { get; }

        /// <summary>
        /// Attaches the callback that will be invoked on completion.
        /// </summary>
        /// <param name="callback">The callback to be attached to the asynchronous operation which result is represented by this awaitable object.</param>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public void OnCompleted(Action callback)
        {
            callback = Continuation.Create(callback);
            if (IsCompleted)
                callback();
            else
                continuation += callback;
        }
    }

    /// <summary>
    /// Represents Future pattern that can be converted into <see cref="Task"/>.
    /// </summary>
    /// <typeparam name="T">The type of task that is supported by awaitable object.</typeparam>
    public abstract class Future<T> : Future
        where T : Task
    {
        /// <summary>
        /// Converts this awaitable object into task of type <typeparamref name="T"/>.
        /// </summary>
        /// <remarks>
        /// This method can cause extra allocation of memory. Do not use it for <c>await</c> scenario.
        /// It is suitable only for interop with <see cref="Task.WhenAll(System.Collections.Generic.IEnumerable{System.Threading.Tasks.Task})"/>
        /// or <see cref="Task.WhenAny(System.Collections.Generic.IEnumerable{System.Threading.Tasks.Task})"/>.
        /// </remarks>
        /// <returns>The task representing the current awaitable object.</returns>
        public abstract T AsTask();
    }
}
