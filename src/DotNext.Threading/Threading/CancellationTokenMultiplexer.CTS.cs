using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading;

using InlinedTokenList = ValueTuple<CancellationTokenRegistration, CancellationTokenRegistration, CancellationTokenRegistration>;

partial class CancellationTokenMultiplexer
{
    private sealed class PooledCancellationTokenSource : LinkedCancellationTokenSource, IResettable
    {
        private static readonly int InlinedListCapacity = GetCapacity<InlinedTokenList>();
        
        private InlinedTokenList inlinedList;
        private int count;
        private CancellationTokenRegistration[]? extraTokens;
        internal PooledCancellationTokenSource? Next;

        public void AddRange(ReadOnlySpan<CancellationToken> tokens)
        {
            // register inlined tokens
            var inlinedRegistrations = inlinedList.AsSpan();
            var inlinedCount = Math.Min(inlinedRegistrations.Length, tokens.Length);

            for (var i = 0; i < inlinedCount; i++)
            {
                inlinedRegistrations[i] = Attach(tokens[i]);
            }

            // register extra tokens
            tokens = tokens.Slice(inlinedCount);
            count = inlinedCount + tokens.Length;
            if (tokens.IsEmpty)
                return;

            if (extraTokens is null || extraTokens.Length < tokens.Length)
            {
                extraTokens = new CancellationTokenRegistration[tokens.Length];
            }

            for (var i = 0; i < tokens.Length; i++)
            {
                extraTokens[i] = Attach(tokens[i]);
            }
        }

        public int Count => count;

        public ref readonly CancellationTokenRegistration this[int index]
        {
            get
            {
                Debug.Assert((uint)index < (uint)count);

                Span<CancellationTokenRegistration> registrations;
                if (index < InlinedListCapacity)
                {
                    registrations = inlinedList.AsSpan();
                }
                else
                {
                    registrations = extraTokens;
                    index -= InlinedListCapacity;
                }

                return ref registrations[index];
            }
        }

        public void Reset()
        {
            inlinedList = default;

            if (extraTokens is not null && count > InlinedListCapacity)
            {
                Array.Clear(extraTokens, 0, count - InlinedListCapacity);
            }

            count = 0;
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