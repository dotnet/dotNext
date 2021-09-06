using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading
{
    /// <summary>
    /// Provides support for asynchronous lazy initialization.
    /// </summary>
    /// <typeparam name="T">The type of object that is being asynchronously initialized.</typeparam>
    public class AsyncLazy<T>
    {
        private const string NotAvailable = "<NotAvailable>";
        private readonly bool resettable;
        private volatile Task<T>? task;
        private Func<Task<T>>? factory;

        /// <summary>
        /// Initializes a new instance of lazy value which is already computed.
        /// </summary>
        /// <param name="value">Already computed value.</param>
        public AsyncLazy(T value)
        {
            resettable = false;
            task = System.Threading.Tasks.Task.FromResult(value);
        }

        /// <summary>
        /// Initializes a new instance of lazy value.
        /// </summary>
        /// <param name="valueFactory">The function used to compute actual value.</param>
        /// <param name="resettable"><see langword="true"/> if previously computed value can be removed and computation executed again when it will be requested; <see langword="false"/> if value can be computed exactly once.</param>
        /// <exception cref="ArgumentException"><paramref name="valueFactory"/> doesn't refer to any method.</exception>
        public AsyncLazy(Func<Task<T>> valueFactory, bool resettable = false)
        {
            if (valueFactory is null)
                throw new ArgumentException(ExceptionMessages.EmptyValueDelegate, nameof(valueFactory));
            this.resettable = resettable;
            factory = valueFactory;
        }

        /// <summary>
        /// Gets a value that indicates whether a value has been computed.
        /// </summary>
        public bool IsValueCreated => (task?.IsCompleted).GetValueOrDefault(false);

        /// <summary>
        /// Gets value if it is already computed.
        /// </summary>
        public Result<T>? Value
        {
            get
            {
                var t = task;
                switch (t?.Status)
                {
                    case TaskStatus.RanToCompletion:
                        return new Result<T>(t.Result);
                    case TaskStatus.Faulted:
                        Debug.Assert(t.Exception is not null);
                        return new Result<T>(t.Exception);
                    default:
                        return null;
                }
            }
        }

        private T RemoveFactory(Task<T> task)
        {
            factory = default; // cleanup factory because it may have captured variables and other objects
            return task.Result;
        }

        /// <summary>
        /// Gets task representing asynchronous computation of lazy value.
        /// </summary>
        public Task<T> Task
        {
            [MethodImpl(MethodImplOptions.Synchronized)]
            get
            {
                if (task is null)
                {
                    Debug.Assert(factory is not null);
                    var t = factory.Invoke();
                    if (!resettable)
                        t = t.ContinueWith(RemoveFactory, default, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Current);
                    task = t;
                }

                return task;
            }
        }

        /// <summary>
        /// Removes already computed value from the current object.
        /// </summary>
        /// <returns><see langword="true"/> if previous value is removed successfully; <see langword="false"/> if value is still computing or this instance is not resettable.</returns>
        [MethodImpl(MethodImplOptions.Synchronized)]
        public bool Reset()
        {
            if (resettable && (task is null || task.IsCompleted))
            {
                task = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets awaiter for the asynchronous operation responsible for computing value.
        /// </summary>
        /// <returns>The task awaiter.</returns>
        public TaskAwaiter<T> GetAwaiter() => Task.GetAwaiter();

        /// <summary>
        /// Configures an awaiter used to await asynchronous lazy initialization.
        /// </summary>
        /// <param name="continueOnCapturedContext"><see langword="true"/> to attempt to marshal the continuation back to the original context captured; otherwise, <see langword="false"/>.</param>
        /// <returns>An object used to await asynchronous lazy initialization.</returns>
        public ConfiguredTaskAwaitable<T> ConfigureAwait(bool continueOnCapturedContext) => Task.ConfigureAwait(continueOnCapturedContext);

        /// <summary>
        /// Returns textual representation of this object.
        /// </summary>
        /// <returns>The string representing this object.</returns>
        public override string? ToString()
        {
            var task = this.task;
            return task?.Status switch
            {
                null => NotAvailable,
                TaskStatus.RanToCompletion => task.Result?.ToString(),
                TaskStatus status => $"<{status}>",
            };
        }
    }
}
