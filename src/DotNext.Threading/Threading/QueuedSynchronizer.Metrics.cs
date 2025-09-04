using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private const string LockTypeMeterAttribute = "dotnext.asynclock.type";
    private static readonly UpDownCounter<int> SuspendedCallersMeter;
    
    private readonly TagList measurementTags;
    
    static QueuedSynchronizer()
    {
        var meter = new Meter("DotNext.Threading.AsyncLock");
        SuspendedCallersMeter = meter.CreateUpDownCounter<int>("suspended-callers-count", description: "Number of Suspended Callers");
    }
    
    /// <summary>
    /// Sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public TagList MeasurementTags
    {
        init
        {
            value.Add(LockTypeMeterAttribute, GetType().Name);
            measurementTags = value;
        }
    }
}