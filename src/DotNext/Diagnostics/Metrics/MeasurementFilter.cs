using System.Diagnostics.Metrics;

namespace DotNext.Diagnostics.Metrics;

/// <summary>
/// Represents a filter of the measurement.
/// </summary>
/// <returns><see langword="true"/> if the measurement must be considered by the observer; otherwise, <see langword="false"/>.</returns>
/// <typeparam name="TInstrument">The type of the measurement source.</typeparam>
public delegate bool MeasurementFilter<in TInstrument>(TInstrument instrument, ReadOnlySpan<KeyValuePair<string, object?>> tags)
where TInstrument: Instrument;