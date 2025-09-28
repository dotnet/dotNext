using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using InlinedTokenList = ValueTuple<CancellationTokenRegistration, CancellationTokenRegistration, CancellationTokenRegistration>;

partial class CancellationTokenMultiplexer
{
    private sealed class PooledCancellationTokenSource : LinkedCancellationTokenSource, IResettable
    {
        private static readonly int InlinedListCapacity = GetCapacity<InlinedTokenList>();
        
        private InlinedTokenList inlinedList;
        private int inlinedTokenCount;
        private List<CancellationTokenRegistration>? extraTokens;
        internal PooledCancellationTokenSource? Next;

        public void AddRange(ReadOnlySpan<CancellationToken> tokens)
        {
            // register inlined tokens
            var inlinedRegistrations = inlinedList.AsSpan();
            inlinedTokenCount = Math.Min(inlinedRegistrations.Length, tokens.Length);

            for (var i = 0; i < inlinedTokenCount; i++)
            {
                inlinedRegistrations[i] = Attach(tokens[i]);
            }

            // register extra tokens
            tokens = tokens.Slice(inlinedTokenCount);
            if (tokens.IsEmpty)
                return;

            if (extraTokens is null)
            {
                extraTokens = new(tokens.Length);
            }
            else
            {
                extraTokens.EnsureCapacity(tokens.Length);
            }

            foreach (var token in tokens)
            {
                extraTokens.Add(Attach(token));
            }
        }

        public int Count => inlinedTokenCount + (extraTokens?.Count ?? 0);

        public ref readonly CancellationTokenRegistration this[int index]
        {
            get
            {
                Debug.Assert((uint)index < (uint)Count);
                
                Span<CancellationTokenRegistration> registrations;
                if (index < InlinedListCapacity)
                {
                    registrations = inlinedList.AsSpan();
                }
                else
                {
                    registrations = CollectionsMarshal.AsSpan(extraTokens);
                    index -= InlinedListCapacity;
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

        private static int GetCapacity<T>()
            where T : struct, ITuple
            => default(T).Length;

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