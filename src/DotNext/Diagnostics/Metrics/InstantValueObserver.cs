using System.Diagnostics.Metrics;
using System.Numerics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Observes the instant value of the instrument.
/// </summary>
/// <typeparam name="TMeasurement">The type of the measurement.</typeparam>
/// <typeparam name="TInstrument">Type of the instrument to observe.</typeparam>
public abstract class InstantValueObserver<TMeasurement, TInstrument> : InstrumentObserver<TMeasurement, TInstrument>, ISupplier<TMeasurement>
    where TMeasurement : unmanaged, INumber<TMeasurement>
    where TInstrument : Instrument<TMeasurement>
{
    private TMeasurement measurement;

    private protected InstantValueObserver(MeasurementFilter<TInstrument>? filter)
        : base(filter)
    {
    }

    /// <inheritdoc/>
    protected sealed override void Record(TMeasurement value)
    {
        Volatile.WriteBarrier();
        measurement = value;
    }

    /// <summary>
    /// Gets instant value of the instrument.
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

    /// <inheritdoc/>
    TMeasurement ISupplier<TMeasurement>.Invoke() => Value;
}