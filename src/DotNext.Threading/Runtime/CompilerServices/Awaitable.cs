using System;
using System.Runtime.CompilerServices;

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
}
