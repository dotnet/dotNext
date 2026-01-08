using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Represents the instrument observer.
/// </summary>
public abstract class InstrumentObserver
{
    /// <summary>
    /// The callback that can be used to complete the observers registered with <see cref="MeterListener"/>.
    /// </summary>
    public static readonly Action<Instrument, object?> ObservationCompletionCallback = Complete;

    private protected InstrumentObserver()
    {
    }

    /// <summary>
    /// Gets a value indicating that the observer doesn't observe the instrument anymore.
    /// </summary>
    public bool IsCompleted { get; private set; }

    private void Complete() => IsCompleted = true;
    
    private static void Complete(Instrument instrument, object? state)
        => (state as InstrumentObserver)?.Complete();
}

/// <summary>
/// Represents the instrument observer.
/// </summary>
/// <typeparam name="TMeasurement">The type of the observable value.</typeparam>
public abstract class InstrumentObserver<TMeasurement> : InstrumentObserver
    where TMeasurement : unmanaged, INumberBase<TMeasurement>
{
    private protected InstrumentObserver()
    {
    }
    
    private protected abstract void Record(Instrument instrument, TMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags);

    private protected static void SetMeasurementEventCallback(MeterListener listener)
        => listener.SetMeasurementEventCallback<TMeasurement>(Record);

    private static void Record(
        Instrument instrument,
        TMeasurement measurement,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        object? state)
        => (state as InstrumentObserver<TMeasurement>)?.Record(instrument, measurement, tags);
}

/// <summary>
/// Represents in-process instrument observer.
/// </summary>
/// <typeparam name="TMeasurement">The type of the observable value.</typeparam>
/// <typeparam name="TInstrument">The source of the observable value.</typeparam>
public abstract class InstrumentObserver<TMeasurement, TInstrument> : InstrumentObserver<TMeasurement>
    where TMeasurement : unmanaged, INumberBase<TMeasurement>
    where TInstrument : Instrument<TMeasurement>
{
    private readonly MeasurementFilter<TInstrument> filter;

    /// <summary>
    /// Initializes a new observer.
    /// </summary>
    /// <param name="filter">The filter to skip the irrelevant measurements.</param>
    protected InstrumentObserver(MeasurementFilter<TInstrument>? filter)
    {
        this.filter = filter ?? True;

        static bool True(TInstrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags) => true;
    }

    private protected sealed override void Record(Instrument instrument, TMeasurement measurement, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        Debug.Assert(instrument is TInstrument);

        Record(Unsafe.As<TInstrument>(instrument), measurement, tags);
    }

    private void Record(TInstrument instrument, TMeasurement value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        if (filter(instrument, tags))
            Record(value);
    }

    /// <summary>
    /// Observes the specified instrument.
    /// </summary>
    /// <param name="instrument">The instrument to observe.</param>
    /// <param name="listener">The listener of the instrument.</param>
    /// <exception cref="ArgumentNullException"><paramref name="instrument"/> or <paramref name="listener"/> is <see langword="null"/>.</exception>
    public void Observe(TInstrument instrument, MeterListener listener)
    {
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(listener);

        SetMeasurementEventCallback(listener);
        listener.EnableMeasurementEvents(instrument, this);
    }

    /// <summary>
    /// Records the value.
    /// </summary>
    /// <param name="value">The captured measurement.</param>
    protected abstract void Record(TMeasurement value);
}