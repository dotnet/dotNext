using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace DotNext.Threading;

partial class AsyncParkingLot
{
    private static readonly UpDownCounter<int> SuspendedCallersMeter;

    static AsyncParkingLot()
    {
        const string meterName = $"DotNext.Threading.{nameof(AsyncParkingLot)}";
        var meter = new Meter(meterName);
        SuspendedCallersMeter = meter.CreateUpDownCounter<int>("suspended-callers-count", description: "Number of Suspended Callers");
    }

    internal readonly TagList measurementTags;
    
    /// <summary>
    /// Sets a list of tags to be associated with each measurement.
    /// </summary>
    [CLSCompliant(false)]
    public TagList MeasurementTags
    {
        init => measurementTags = value;
    }
}