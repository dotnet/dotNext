namespace RaftNode;

internal interface IValueProvider
{
    long Value { get; }

    Task UpdateValueAsync(long value, TimeSpan timeout, CancellationToken token);
}