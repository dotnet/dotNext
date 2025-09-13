using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class CancellationTokenMultiplexer
{
    private sealed class PooledCancellationTokenSource : LinkedCancellationTokenSource, IResettable
    {
        private const int Capacity = 3;
        private (CancellationTokenRegistration, CancellationTokenRegistration, CancellationTokenRegistration) inlinedList;
        private byte inlinedTokenCount;
        private List<CancellationTokenRegistration>? extraTokens;
        internal PooledCancellationTokenSource? Next;

        public void Add(CancellationToken token)
            => Add() = Attach(token);

        private ref CancellationTokenRegistration Add()
        {
            Span<CancellationTokenRegistration> registrations;
            int index;
            if (inlinedTokenCount < Capacity)
            {
                index = inlinedTokenCount++;
                registrations = inlinedList.AsSpan();
            }
            else
            {
                extraTokens ??= new();
                index = extraTokens.Count;
                extraTokens.Add(default);
                registrations = CollectionsMarshal.AsSpan(extraTokens);
            }

            return ref registrations[index];
        }

        public int Count => inlinedTokenCount + extraTokens?.Count ?? 0;

        public ref CancellationTokenRegistration this[int index]
        {
            get
            {
                Span<CancellationTokenRegistration> registrations;
                if (index < Capacity)
                {
                    registrations = inlinedList.AsSpan();
                }
                else
                {
                    registrations = CollectionsMarshal.AsSpan(extraTokens);
                    index -= Capacity;
                }

                return ref registrations[index];
            }
        }

        public void Reset()
        {
            inlinedTokenCount = 0;
            inlinedList = default;
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