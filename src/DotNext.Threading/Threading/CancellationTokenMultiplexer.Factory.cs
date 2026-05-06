namespace DotNext.Threading;

using Patterns;

partial struct CancellationTokenMultiplexer
{
    internal static IMultiplexedCancellationTokenSource Create(ReadOnlySpan<CancellationToken> tokens)
        => CreateSource<IMultiplexedCancellationTokenSource, CancellationTokenSourceFactory>(CancellationTokenSourceFactory.Instance, tokens);
    
    private sealed class CancellationTokenSourceFactory : IMultiplexedCancellationTokenSourceFactory<IMultiplexedCancellationTokenSource>, ISingleton<CancellationTokenSourceFactory>
    {
        public static CancellationTokenSourceFactory Instance { get; } = new();

        private CancellationTokenSourceFactory()
        {
        }

        static IMultiplexedCancellationTokenSource IMultiplexedCancellationTokenSourceFactory<IMultiplexedCancellationTokenSource>.Create(
            CancellationToken token)
            => IMultiplexedCancellationTokenSource.Create(token);

        static IMultiplexedCancellationTokenSource IMultiplexedCancellationTokenSourceFactory<IMultiplexedCancellationTokenSource>.Empty
            => IMultiplexedCancellationTokenSource.Create(canceled: false);

        IMultiplexedCancellationTokenSource IMultiplexedCancellationTokenSourceFactory<IMultiplexedCancellationTokenSource>.Create(
            ReadOnlySpan<CancellationToken> tokens)
        {
            var source = new PooledCancellationTokenSource();
            source.AddRange(tokens);
            return source;
        }
    }
    
    private static TSource CreateSource<TSource, TFactory>(scoped TFactory factory, scoped ReadOnlySpan<CancellationToken> tokens)
        where TSource : IMultiplexedCancellationTokenSource
        where TFactory : IMultiplexedCancellationTokenSourceFactory<TSource>, allows ref struct
    {
        TSource scope;
        switch (tokens)
        {
            case []:
                scope = TFactory.Empty;
                break;
            case [var token]:
                scope = TFactory.Create(token);
                break;
            case [var token1, var token2]:
                if (!token1.CanBeCanceled || token1 == token2)
                {
                    scope = TFactory.Create(token2);
                }
                else if (!token2.CanBeCanceled)
                {
                    scope = TFactory.Create(token1);
                }
                else
                {
                    goto default;
                }

                break;
            default:
                scope = factory.Create(tokens);
                break;
        }

        return scope;
    }
    
    private interface IMultiplexedCancellationTokenSourceFactory<out TSource>
        where TSource : IMultiplexedCancellationTokenSource
    {
        public static abstract TSource Create(CancellationToken token);
        
        public static abstract TSource Empty { get; }

        TSource Create(ReadOnlySpan<CancellationToken> tokens);
    }
}