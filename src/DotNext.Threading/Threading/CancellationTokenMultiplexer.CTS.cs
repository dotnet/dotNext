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

        public void Add(CancellationToken token)
            => Add(Attach(token));

        private void Add(CancellationTokenRegistration registration)
        {
            if (inlinedTokenCount < InlinedListCapacity)
            {
                Unsafe.Add(ref FirstInlinedRegistration, inlinedTokenCount++) = registration;
            }
            else
            {
                extraTokens ??= new();
                extraTokens.Add(registration);
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ref CancellationTokenRegistration FirstInlinedRegistration
            => ref Unsafe.As<InlinedTokenList, CancellationTokenRegistration>(ref inlinedList);

        public int Count => inlinedTokenCount + (extraTokens?.Count ?? 0);

        public ref CancellationTokenRegistration this[int index]
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