using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Represents builder of <seealso cref="MeterListener"/> that is responsible for tracking instrument observers.
/// </summary>
public sealed class MeterListenerBuilder
{
    private Action<Instrument, MeterListener>? registration;

    /// <summary>
    /// Enables observation of the instrument specified by the filter.
    /// </summary>
    /// <param name="filter">The filter for the instrument.</param>
    /// <param name="observer">The observer.</param>
    /// <typeparam name="TMeasurement">The type of the measurement.</typeparam>
    /// <typeparam name="TInstrument">The type of the instrument.</typeparam>
    /// <returns>This builder.</returns>
    public MeterListenerBuilder Observe<TMeasurement, TInstrument>(Predicate<TInstrument> filter, InstrumentObserver<TMeasurement, TInstrument> observer)
        where TMeasurement : struct
        where TInstrument : Instrument<TMeasurement>
    {
        registration += new InstrumentHandler<TMeasurement, TInstrument>(filter, observer).Publish;
        return this;
    }

    /// <summary>
    /// Builds configured <see cref="MeterListener"/>.
    /// </summary>
    /// <returns>The configured listener.</returns>
    public MeterListener Build() => new()
    {
        InstrumentPublished = registration,
        MeasurementsCompleted = InstrumentObserver.ObservationCompletionCallback,
    };
    
    private sealed class InstrumentHandler<TMeasurement, TInstrument>(Predicate<TInstrument> filter,
        InstrumentObserver<TMeasurement, TInstrument> observer)
        where TMeasurement : struct
        where TInstrument : Instrument<TMeasurement>
    {
        public void Publish(Instrument instrument, MeterListener listener)
        {
            if (instrument is TInstrument typedInstrument && filter(typedInstrument))
            {
                observer.Observe(typedInstrument, listener);
            }
        }
    }
}