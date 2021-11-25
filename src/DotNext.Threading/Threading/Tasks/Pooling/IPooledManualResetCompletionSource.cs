using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks.Pooling;

internal interface IPooledManualResetCompletionSource<TNode> : IValueTaskSource
    where TNode : ManualResetCompletionSource, IPooledManualResetCompletionSource<TNode>
{
    ref Action<TNode>? OnConsumed { get; }
}