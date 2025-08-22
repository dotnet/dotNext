using System.Diagnostics;

namespace DotNext.Net.Multiplexing;

using Threading;

internal delegate MultiplexedStream? MultiplexedStreamFactory(AsyncAutoResetEvent transportSignal, in TagList measurementTags);