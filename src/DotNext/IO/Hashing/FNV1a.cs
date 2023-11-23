using System.Diagnostics;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.IO.Hashing;

using Intrinsics = Runtime.Intrinsics;

/// <summary>
/// Represents FNV-1a hash algorithm.
/// </summary>
/// <typeparam name="THash">The type representing hash value.</typeparam>
/// <typeparam name="TParameters">The parameters of the algorithm.</typeparam>
/// <seealso href="http://www.isthe.com/chongo/tech/comp/fnv/#FNV-1a">FNV-1a</seealso>
/// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
public class FNV1a<THash, TParameters>(bool salted = false) : NonCryptographicHashAlgorithm(Unsafe.SizeOf<THash>()), IResettable
    where THash : unmanaged, IBinaryNumber<THash>
    where TParameters : notnull, IFNV1aParameters<THash>
{
    private State state = new(salted);

    /// <inheritdoc/>
    public sealed override void Reset() => state = new(salted);

    /// <inheritdoc/>
    public sealed override void Append(ReadOnlySpan<byte> source)
        => state.Append(source);

    /// <summary>
    /// Appends the contents of source to the data already processed for the current hash computation.
    /// </summary>
    /// <typeparam name="T">The type of the span elements.</typeparam>
    /// <param name="source">The data to process.</param>
    public void Append<T>(ReadOnlySpan<T> source)
        where T : unmanaged
        => Hash(ref state, source);

    /// <summary>
    /// Appends the contents of unmanaged memory to the data already processed for the current hash computation.
    /// </summary>
    /// <param name="address">The address of the unmanaged memory.</param>
    /// <param name="length">The length of the unmanaged memory block, in bytes.</param>
    [CLSCompliant(false)]
    public unsafe void Append(void* address, nuint length)
        => state.Append(ref Unsafe.AsRef<byte>(address), length);

    /// <summary>
    /// Appends the value to the data already processed for the current hash computation.
    /// </summary>
    /// <param name="value">The value to be hashed.</param>
    public void Append(THash value)
        => state.Append(value);

    /// <inheritdoc/>
    protected sealed override void GetCurrentHashCore(Span<byte> destination)
        => state.GetValue(destination);

    /// <summary>
    /// Gets the current computed hash value without modifying accumulated state.
    /// </summary>
    /// <returns>The hash value for the data already provided.</returns>
    public new THash GetCurrentHash() => state.Value;

    private static void Hash<T>(ref State hash, ReadOnlySpan<T> data)
        where T : unmanaged
    {
        if (typeof(T) == typeof(byte))
        {
            hash.Append(Intrinsics.ReinterpretCast<T, byte>(data));
        }
        else if (Intrinsics.AreCompatible<T, THash>())
        {
            hash.Append(Intrinsics.ReinterpretCast<T, THash>(data));
        }
        else
        {
            hash.Append(data);
        }
    }

    /// <summary>
    /// Computes hash code over the span of elements.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="data">The data to be hashed.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>The computed FNV-1a hash.</returns>
    public static THash Hash<T>(ReadOnlySpan<T> data, bool salted = false)
        where T : unmanaged
    {
        var hash = new State(salted);
        Hash(ref hash, data);
        return hash.Value;
    }

    /// <summary>
    /// Computes hash code over elements returned by vector accessor.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the vector.</typeparam>
    /// <typeparam name="TIndex">The type that is used as identifier of the elements in the vector.</typeparam>
    /// <param name="accessor">The delegate that provided access to the element of type <typeparamref name="THash"/> at the given index.</param>
    /// <param name="count">The number of elements in the vector.</param>
    /// <param name="arg">The argument to be passed to the accessor.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>The computed FNV-1a hash.</returns>
    public static THash Hash<T, TIndex>(Func<T, TIndex, THash> accessor, TIndex count, T arg, bool salted = false)
        where TIndex : notnull, IComparisonOperators<TIndex, TIndex, bool>, IAdditiveIdentity<TIndex, TIndex>, IIncrementOperators<TIndex>
    {
        ArgumentNullException.ThrowIfNull(accessor);

        var hash = new State(salted);
        for (var i = TIndex.AdditiveIdentity; i < count; i++)
        {
            hash.Append(accessor(arg, i));
        }

        return hash.Value;
    }

    /// <summary>
    /// Computes hash code for the specified block of unmanaged memory.
    /// </summary>
    /// <param name="address">The address of the unmanaged memory.</param>
    /// <param name="length">The length of the unmanaged memory block, in bytes.</param>
    /// <param name="salted"><see langword="true"/> to include randomized salt data into hashing; <see langword="false"/> to use data from memory only.</param>
    /// <returns>The computed FNV-1a hash.</returns>
    [CLSCompliant(false)]
    public static unsafe THash Hash(void* address, nuint length, bool salted = false)
    {
        var hash = new State(salted);
        hash.Append(ref Unsafe.AsRef<byte>(address), length);
        return hash.Value;
    }

    /// <summary>
    /// Computes a hash for a value of type <typeparamref name="THash"/>.
    /// </summary>
    /// <param name="data">The data to be hashed.</param>
    /// <returns>The computed FNV-1a hash.</returns>
    public static THash Hash(THash data)
    {
        State.Append(ref data, data);
        return data;
    }

    [StructLayout(LayoutKind.Auto)]
    private unsafe struct State
    {
        private int bufferSize;
        private THash buffer, hash;

        internal State(bool salted)
        {
            hash = TParameters.Offset;

            if (salted)
                Append(ref hash, THash.CreateTruncating(RandomExtensions.BitwiseHashSalt));
        }

        internal readonly THash Value
        {
            get
            {
                var result = hash;
                if (HasBufferedData)
                    Append(ref result, buffer);

                return result;
            }
        }

        internal readonly void GetValue(Span<byte> output)
        {
            var hash = Value;
            Span.AsReadOnlyBytes(in hash).CopyTo(output);
        }

        private Span<byte> RemainingBuffer
        {
            get
            {
                ref var bufferPtr = ref Unsafe.Add(ref Unsafe.As<THash, byte>(ref buffer), bufferSize);
                return MemoryMarshal.CreateSpan(ref bufferPtr, sizeof(THash) - bufferSize);
            }
        }

        private void Bufferize(ReadOnlySpan<byte> data)
        {
            data.CopyTo(RemainingBuffer);
            bufferSize += data.Length;
        }

        internal void Append(ref byte data, nuint length)
        {
            var remaining = RemainingBuffer;
            if (remaining.Length < sizeof(THash) && (uint)remaining.Length <= length)
            {
                data = ref Unsafe.Add(ref data, Append(remaining, ref data, ref length));
            }

            for (; length >= (uint)sizeof(THash); length -= (uint)sizeof(THash), data = ref Unsafe.Add(ref data, sizeof(THash)))
            {
                Append(Unsafe.ReadUnaligned<THash>(in data));
            }

            if (length > 0)
            {
                Debug.Assert(length < (uint)sizeof(THash));

                Bufferize(MemoryMarshal.CreateReadOnlySpan(ref data, (int)length));
            }
        }

        private int Append(Span<byte> remaining, ref byte data, ref nuint length)
        {
            var input = MemoryMarshal.CreateReadOnlySpan(ref data, remaining.Length);
            input.CopyTo(remaining);
            Flush();
            length -= (uint)input.Length;
            return input.Length;
        }

        internal void Append(ReadOnlySpan<byte> data)
            => Append(ref MemoryMarshal.GetReference(data), (nuint)data.Length);

        internal void Append(ReadOnlySpan<THash> data)
        {
            foreach (var element in data)
                Append(element);
        }

        internal unsafe void Append<T>(ReadOnlySpan<T> data)
            where T : unmanaged
        {
            for (int maxSize = int.MaxValue / sizeof(T), size; !data.IsEmpty; data = data.Slice(size))
            {
                size = Math.Min(maxSize, data.Length);
                Append(ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(data)), (uint)size * (uint)sizeof(T));
            }
        }

        private readonly bool HasBufferedData => bufferSize > 0;

        private void Flush()
        {
            Debug.Assert(HasBufferedData);

            Append(buffer);
            bufferSize = 0;
            buffer = default;
        }

        internal static void Append(ref THash hash, THash data)
            => hash = (hash ^ data) * TParameters.Prime;

        internal void Append(THash data)
            => Append(ref hash, data);
    }
}