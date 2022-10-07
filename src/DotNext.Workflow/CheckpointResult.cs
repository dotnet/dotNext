using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Workflow;

[StructLayout(LayoutKind.Auto)]
public readonly struct CheckpointResult
{
    [StructLayout(LayoutKind.Auto)]
    public struct Awaiter : INotifyCompletion
    {
        private Task? result;

        public bool IsCompleted => false;

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

            if (completedSynchronously = t.IsCompleted)
            {
                t = TryGetException(t, out var error) ? Task.FromException(error) : null;
            }

            this.result = t;
            return completedSynchronously;
        }

        public void GetResult()
        {
            if (result is not null && TryGetException(result, out var error))
                throw error;
        }

        /// <inheritdoc />
        void INotifyCompletion.OnCompleted(Action continuation) => throw new NotImplementedException();
    }

    public Awaiter GetAwaiter() => default;
}