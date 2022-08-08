namespace DotNext.Threading;

/// <summary>
/// Indicates that the exchange has been terminated by the one of
/// participants.
/// </summary>
public sealed class ExchangeTerminatedException : PendingTaskInterruptedException
{
    internal ExchangeTerminatedException(Exception? exception)
        : base(ExceptionMessages.TerminatedExchange, exception)
    {
    }
}