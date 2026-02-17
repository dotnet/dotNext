using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

/// <summary>
/// Extends <see cref="Monitor"/> and <see cref="System.Threading.Lock"/> types.
/// </summary>
public static class LockExtensions
{
    /// <summary>
    /// Tries to acquire an exclusive lock on the specified object with cancellation support.
    /// </summary>
    /// <param name="lock">The object on which to acquire the lock.</param>
    /// <param name="timeout">Time to wait for the lock.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <param name="throwOnCancellation">
    /// <see langword="true"/> to throw <see cref="OperationCanceledException"/> if <paramref name="token"/> is canceled during the lock acquisition;
    /// <see langword="false"/> to return <see langword="false"/>.
    /// </param>
    /// <returns><see langword="true"/> if the monitor acquired successfully; <see langword="false"/> if timeout occurred or <paramref name="token"/> canceled.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="lock"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is invalid.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="token"/> interrupts lock acquisition and <paramref name="throwOnCancellation"/> is <see langword="true"/>.</exception>
    public static bool TryEnter(this System.Threading.Lock @lock, TimeSpan timeout, CancellationToken token, bool throwOnCancellation = false)
    {
        Timeout.Validate(timeout);

        var result = false;
        if (token.CanBeCanceled)
        {
            var registration = token.UnsafeRegister(Interrupt, Thread.CurrentThread);
            try
            {
                result = @lock.TryEnter(timeout);
            }
            catch (ThreadInterruptedException e) when (token.IsCancellationRequested)
            {
                if (throwOnCancellation)
                    throw new OperationCanceledException(e.Message, e, token);

                goto exit;
            }
            finally
            {
                registration.Dispose();
            }

            // make sure that the interruption was not called on this thread concurrently with registration.Dispose()
            if (token.IsCancellationRequested)
            {
                try
                {
                    Thread.Sleep(0); // reset interrupted state
                }
                catch (ThreadInterruptedException)
                {
                    // suspend exception
                }
            }

            static void Interrupt(object? thread)
            {
                Debug.Assert(thread is Thread);

                Unsafe.As<Thread>(thread).Interrupt();
            }
        }
        else
        {
            result = @lock.TryEnter(timeout);
        }

        exit:
        return result;
    }
}