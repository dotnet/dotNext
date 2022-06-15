namespace DotNext.Runtime;

internal interface IGCCallback
{
    void StopTracking();
}

internal sealed class GCIntermediateReference : WeakReference
{
    internal GCIntermediateReference(object obj)
        : base(obj, obj is IGCCallback)
    {
    }

    internal void Clear()
    {
        switch (Target)
        {
            case null:
                break;
            case IGCCallback tracker:
                tracker.StopTracking();
                goto default;
            default:
                // Change target only if it is alive (not null).
                // Otherwise, CLR GC thread may crash with InvalidOperationException
                // because underlying GC handle is no longer valid
                try
                {
                    Target = null;
                }
                catch (InvalidOperationException)
                {
                    // suspend exception, the weak reference is already finalized
                }

                break;
        }
    }
}