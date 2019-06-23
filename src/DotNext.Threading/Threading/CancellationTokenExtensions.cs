using System;
using System.Threading;
using System.Threading.Tasks;

namespace DotNext.Threading
{
    using Tasks;
    using True = Generic.BooleanConst.True;

    /// <summary>
    /// Represents extension methods for type <see cref="CancellationToken"/>.
    /// </summary>
    public static class CancellationTokenExtensions
    {
        /// <summary>
        /// Obtains a task that can be used to await token cancellation.
        /// </summary>
        /// <param name="token">The token to be converted into task.</param>
        /// <param name="completeAsCanceled"><see langword="true"/> to complete task in <see cref="TaskStatus.Canceled"/> state; <see langword="false"/> to complete task in <see cref="TaskStatus.RanToCompletion"/> state.</param>
        /// <returns>A task representing token state.</returns>
        /// <exception cref="ArgumentException"><paramref name="token"/> doesn't support cancellation.</exception>
        public static Task AsTask(this CancellationToken token, bool completeAsCanceled = false)
        {
            if (!token.CanBeCanceled)
                throw new ArgumentException(ExceptionMessages.TokenNotCancelable, nameof(token));
            if (token.IsCancellationRequested)
                return completeAsCanceled ? Task.FromCanceled(token) : Task.CompletedTask;
            var task = new CancelableTaskCompletionSource<bool>(ref token, TaskCreationOptions.None).Task;
            return completeAsCanceled ? task : task.OnCanceled<bool, True>();
        }
    }
}
