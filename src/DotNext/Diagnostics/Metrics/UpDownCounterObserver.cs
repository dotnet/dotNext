using System.Diagnostics.Metrics;
using System.Numerics;
using System.Runtime.CompilerServices;

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
    public TMeasurement Value => VolatileRead(ref measurement);

    private static void Aggregate(ref TMeasurement measurement, TMeasurement value)
    {
        if (typeof(TMeasurement) == typeof(int))
            Interlocked.Add(ref Unsafe.As<TMeasurement, int>(ref measurement), Unsafe.BitCast<TMeasurement, int>(value));

        if (typeof(TMeasurement) == typeof(long))
            Interlocked.Add(ref Unsafe.As<TMeasurement, long>(ref measurement), Unsafe.BitCast<TMeasurement, long>(value));
    }

    /// <inheritdoc />
    protected override void Record(TMeasurement value)
        => Aggregate(ref measurement, value);

    /// <inheritdoc />
    TMeasurement ISupplier<TMeasurement>.Invoke() => Value;
}