using System.Diagnostics.Metrics;
using System.Numerics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Observes instant value of <seealso cref="Histogram{T}"/> instrument.
/// </summary>
/// <param name="filter">The filter to skip the irrelevant measurements.</param>
/// <typeparam name="TMeasurement">The type of the measurement.</typeparam>
public sealed class HistogramObserver<TMeasurement>(MeasurementFilter<Histogram<TMeasurement>>? filter = null)
    : InstrumentObserver<TMeasurement, Histogram<TMeasurement>>(filter)
    where TMeasurement : unmanaged, IBinaryInteger<TMeasurement>
{
    private TMeasurement measurement;

    /// <inheritdoc/>
    protected override void Record(TMeasurement value)
        => VolatileWrite(ref measurement, value);
}