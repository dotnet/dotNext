using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

public static partial class Atomic
{
    extension(Interlocked)
    {
        /// <summary>
        /// Atomically sets <see langword="true"/> value if the
        /// current value is <see langword="false"/>.
        /// </summary>
        /// <param name="location">The destination, whose value is compared with comparand and possibly replaced.</param>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool FalseToTrue(ref bool location) => !Interlocked.CompareExchange(ref location, true, false);

        /// <summary>
        /// Atomically sets <see langword="false"/> value if the
        /// current value is <see langword="true"/>.
        /// </summary>
        /// <param name="location">The destination, whose value is compared with comparand and possibly replaced.</param>
        /// <returns><see langword="true"/> if current value is modified successfully; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TrueToFalse(ref bool location) => Interlocked.CompareExchange(ref location, false, true);

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <returns>Negation result.</returns>
        public static bool NegateAndGet(ref bool location) => UpdateAndGet(ref location, new Negation());

        /// <summary>
        /// Negates currently stored value atomically.
        /// </summary>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <returns>The original value before negation.</returns>
        public static bool GetAndNegate(ref bool location) => GetAndUpdate(ref location, new Negation());

        internal static void Acquire(ref bool location)
        {
            if (Interlocked.Exchange(ref location, true))
                Contention(ref location);

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void Contention(ref bool location)
            {
                var spinner = new SpinWait();
                do
                {
                    spinner.SpinOnce();
                } while (Interlocked.Exchange(ref location, true));
            }
        }

        internal static void Release(ref bool location) => Volatile.Write(ref location, false);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct Negation : ISupplier<bool, bool>
    {
        bool ISupplier<bool, bool>.Invoke(bool value) => !value;
    }
}