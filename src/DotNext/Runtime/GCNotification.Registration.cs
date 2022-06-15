using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Runtime;

public partial class GCNotification
{
    /// <summary>
    /// Represents callback registration.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly struct Registration : IDisposable
    {
        private readonly GCIntermediateReference reference;

        internal Registration(IGCCallback callback)
        {
            Debug.Assert(callback is not null);

            reference = new(callback);
        }

        /// <summary>
        /// Stops listening for GC notification.
        /// </summary>
        public void Dispose() => reference?.Clear();
    }
}