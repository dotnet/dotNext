using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents base class for ad-hoc awaitable objects in this library.
    /// </summary>
    public abstract class Awaitable
    {
        private protected Action continuation;

        private protected Awaitable() { }

        /// <summary>
        /// Determines whether asynchronous operation referenced by this object is already completed.
        /// </summary>
        public abstract bool IsCompleted { get; }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private protected void AddContinuation(Action callback)
        {
            callback = Continuation.Create(callback);
            if (IsCompleted)
                callback();
            else
                continuation += callback;
        }
    }

    /// <summary>
    /// Represents ad-hoc awaitable object that can be converted into <see cref="Task"/>.
    /// </summary>
    /// <typeparam name="T">The type of task that is supported by awaitable object.</typeparam>
    public abstract class Awaitable<T> : Awaitable
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
