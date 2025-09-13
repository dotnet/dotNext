using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class CancellationTokenMultiplexer
{
    private sealed class PooledCancellationTokenSource : LinkedCancellationTokenSource
    {
        private const int Capacity = 3;
        private (CancellationTokenRegistration, CancellationTokenRegistration, CancellationTokenRegistration) inlineList;
        private List<CancellationTokenRegistration>? extraTokens;
        private int tokenCount;
        internal PooledCancellationTokenSource? Next;

        public void Add(CancellationToken token)
            => Add() = Attach(token);

        private ref CancellationTokenRegistration Add()
        {
            Span<CancellationTokenRegistration> registrations;
            var index = tokenCount;
            if (tokenCount < Capacity)
            {
                registrations = inlineList.AsSpan();
            }
            else
            {
                extraTokens ??= new();
                extraTokens.Add(default);
                registrations = CollectionsMarshal.AsSpan(extraTokens);
                index -= Capacity;
            }

            tokenCount++;
            return ref registrations[index];
        }

        public int Count => tokenCount;

        public ref CancellationTokenRegistration this[int index]
        {
            get
            {
                Span<CancellationTokenRegistration> registrations;
                if (index < Capacity)
                {
                    registrations = inlineList.AsSpan();
                }
                else
                {
                    registrations = CollectionsMarshal.AsSpan(extraTokens);
                    index -= Capacity;
                }

                return ref registrations[index];
            }
        }

        public void Clear()
        {
            inlineList = default;
            extraTokens?.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                extraTokens = null; // help GC
            }

            base.Dispose(disposing);
        }
    }
}