using System.Diagnostics.Metrics;
using System.Numerics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Observes instant value of <see cref="Gauge{T}"/> instrument.
/// </summary>
/// <param name="filter">The filter to skip the irrelevant measurements.</param>
/// <typeparam name="TMeasurement">The type of the measurement.</typeparam>
public sealed class GaugeObserver<TMeasurement>(MeasurementFilter<Gauge<TMeasurement>>? filter = null)
    : InstantValueObserver<TMeasurement, Gauge<TMeasurement>>(filter)
    where TMeasurement : unmanaged, INumber<TMeasurement>;