using System;
using System.Threading;
using System.Threading.Tasks;

namespace RaftNode
{
    internal interface IValueProvider
    {
        long Value { get; }

        Task UpdateValueAsync(long value, TimeSpan timeout, CancellationToken token);
    }
}
