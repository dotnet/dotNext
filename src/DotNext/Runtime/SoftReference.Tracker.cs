namespace DotNext.Runtime;

partial class SoftReference<T>
{
    // tracks generation of Target in each GC collection using Finalizer as a callback
    private sealed class Tracker(SoftReference<T> parent, SoftReferenceOptions options) : IGCCallback
    {
        // the number of Gen2 collections at the moment when the target has been observed in Gen2 for the first time
        private int gen2CollectionCount;

        public required int Gen2CollectionCount
        {
            init => gen2CollectionCount = value;
        }

        ~Tracker()
        {
            // Thread safety: preserve order of fields
            if (parent.strongRef is not { } target || parent.trackerRef is not { } thisRef)
            {
                // do nothing
            }
            else if (ReferenceEquals(this, thisRef.Target) && options.KeepTracking(target, ref gen2CollectionCount))
            {
                GC.ReRegisterForFinalize(this);
            }
            else
            {
                // Downgrade to weak reference. thisRef tracks resurrection to observe this tracker during finalization,
                // so it cannot be reused: the target must not be observable after its own finalization
                var weakRef = new GCIntermediateReference(target);
                if (!ReferenceEquals(Interlocked.CompareExchange(ref parent.trackerRef, weakRef, thisRef), thisRef))
                    weakRef.Clear(); // soft reference is cleared concurrently

                parent.strongRef = null;
            }
        }
    }
}