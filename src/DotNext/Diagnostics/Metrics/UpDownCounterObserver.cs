using System.Diagnostics.Metrics;
using System.Numerics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Observes instant value of the <seealso cref="UpDownCounter{T}"/> instrument.
/// </summary>
/// <param name="filter">The filter to skip the irrelevant measurements.</param>
/// <typeparam name="TMeasurement">The type of the measurement.</typeparam>
public sealed class UpDownCounterObserver<TMeasurement>(MeasurementFilter<UpDownCounter<TMeasurement>>? filter = null) : InstrumentObserver<TMeasurement, UpDownCounter<TMeasurement>>(filter), ISupplier<TMeasurement>
    where TMeasurement : unmanaged, ISignedNumber<TMeasurement>, IBinaryInteger<TMeasurement>
{
    private TMeasurement measurement;

    /// <summary>
    /// Gets the instant value of the counter.
    /// </summary>
    public TMeasurement Value
    {
        get
        {
            var result = measurement;
            Volatile.ReadBarrier();
            return result;
        }
    }

    /// <inheritdoc />
    protected override void Record(TMeasurement value)
    {
        for (TMeasurement current = measurement, tmp;; current = tmp)
        {
            tmp = Interlocked.CompareExchange(ref measurement, current + TMeasurement.One, current);
            if (tmp == current)
                break;
        }
    }

    /// <inheritdoc />
    TMeasurement ISupplier<TMeasurement>.Invoke() => Value;
}