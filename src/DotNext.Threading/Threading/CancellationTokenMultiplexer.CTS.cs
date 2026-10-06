using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial struct CancellationTokenMultiplexer
{
    private sealed partial class PooledCancellationTokenSource : LinkedCancellationTokenSource, IResettable
    {
        private const int InlinedListCapacity = 3;

        private InlineArray3<CancellationTokenRegistration> inlinedList;
        private int count;
        private CancellationTokenRegistration[]? extraTokens;

        public void DetachLinkedTokens<TCleanup>()
            where TCleanup : struct, IRegistrationCleanup, allows ref struct
        {
            for (var i = 0; i < count; i++)
            {
                TCleanup.Clear(in this[i]);
            }
        }

        public void AddRange(ReadOnlySpan<CancellationToken> tokens)
        {
            // register inlined tokens
            var inlinedCount = int.Min(InlinedListCapacity, tokens.Length);

            for (var i = 0; i < inlinedCount; i++)
            {
                inlinedList[i] = Attach(tokens[i]);
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
                    registrations = inlinedList;
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
            CancellationOrigin = CancellationToken.None;
            callbackOrSentinel = callbackState = schedulingContext = null;
            context = null;
            pool = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                DetachLinkedTokens<CancelRegistration>();
                inlinedList = default;
                extraTokens = null; // help GC
            }

            count = 0;
            base.Dispose(disposing);
        }
    }
    
    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct MultiplexedCancellationTokenSourceFactory(CancellationTokenMultiplexer multiplexer) : IMultiplexedCancellationTokenSourceFactory<Scope>
    {
        static Scope IMultiplexedCancellationTokenSourceFactory<Scope>.Create(CancellationToken token)
            => new(token);

        static Scope IMultiplexedCancellationTokenSourceFactory<Scope>.Empty => default;

        Scope IMultiplexedCancellationTokenSourceFactory<Scope>.Create(scoped ReadOnlySpan<CancellationToken> tokens)
            => new(multiplexer, tokens);
    }
    
    private interface IRegistrationCleanup
    {
        public static abstract void Clear(ref readonly CancellationTokenRegistration registration);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct DisposeRegistration : IRegistrationCleanup
    {
        static void IRegistrationCleanup.Clear(ref readonly CancellationTokenRegistration registration)
            => registration.Dispose();
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CancelRegistration : IRegistrationCleanup
    {
        static void IRegistrationCleanup.Clear(ref readonly CancellationTokenRegistration registration)
            => registration.Unregister();
    }
}