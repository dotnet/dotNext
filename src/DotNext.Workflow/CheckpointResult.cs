using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Workflow;

/// <summary>
/// Represents checkpoint processing result.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly struct CheckpointResult
{
    [StructLayout(LayoutKind.Auto)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct Awaiter : ICriticalNotifyCompletion
    {
        private readonly StrongBox<Task?> result;

        public Awaiter() => result = new();

        public bool IsCompleted => result is not null;

        private static bool TryGetException(Task t, [NotNullWhen(true)] out InstancePersistenceException? error)
        {
            try
            {
                t.GetAwaiter().GetResult();
                error = null;
                return false;
            }
            catch (InstancePersistenceException e)
            {
                error = e;
                return true;
            }
            catch (Exception e)
            {
                error = new InstancePersistenceException(e);
                return true;
            }
        }

        internal bool SetResult([DisallowNull] Task? t)
        {
            bool completedSynchronously;

            if ((completedSynchronously = t.IsCompleted) && TryGetException(t, out var error))
            {
                t = Task.FromException(error);
            }

            this.result.Value = t;
            return completedSynchronously;
        }

        /// <summary>
        /// Gets a value indicating how the workflow is restored.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the workflow is resumed locally;
        /// <see langword="false"/> if the workflow is restored from the checkpoint.
        /// </returns>
        public bool GetResult()
            => result?.Value is { IsCompleted: true } t && (TryGetException(t, out var error) ? throw error : true);

        /// <inheritdoc />
        void INotifyCompletion.OnCompleted(Action continuation) => throw new NotImplementedException();

        /// <inheritdoc />
        void ICriticalNotifyCompletion.UnsafeOnCompleted(Action continuation) => throw new NotImplementedException();
    }

    /// <summary>
    /// Gets awaiter for the checkpoint processing operation.
    /// </summary>
    /// <returns>The awaiter.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Awaiter GetAwaiter() => new();
}