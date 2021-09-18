using System.Threading.Tasks.Sources;

namespace DotNext.Threading.Tasks.Pooling;

internal interface IPooledManualResetCompletionSource<out TNode> : IValueTaskSource
    where TNode : ManualResetCompletionSource
{
    public static abstract TNode CreateSource(Action<TNode> backToPool);
}