using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Buffers.Binary;

/// <summary>
/// Represents a value of blittable type as <see cref="IBinaryFormattable{TSelf}"/>.
/// </summary>
/// <typeparam name="T">The blittable type.</typeparam>
[StructLayout(LayoutKind.Auto)]
public struct Blittable<T> : IBinaryFormattable<Blittable<T>>
    where T : unmanaged
{
    /// <summary>
    /// A value of blittable type.
    /// </summary>
    required public T Value;

    /// <inheritdoc/>
    readonly void IBinaryFormattable<Blittable<T>>.Format(Span<byte> output)
        => Span.AsReadOnlyBytes(in Value).CopyTo(output);

    /// <inheritdoc cref="IBinaryFormattable{TSelf}.Size"/>
    static int IBinaryFormattable<Blittable<T>>.Size => Unsafe.SizeOf<T>();

    /// <inheritdoc cref="IBinaryFormattable{TSelf}.Parse(ReadOnlySpan{byte})"/>
    static Blittable<T> IBinaryFormattable<Blittable<T>>.Parse(ReadOnlySpan<byte> input)
    {
        Unsafe.SkipInit(out Blittable<T> result);
        input.CopyTo(Span.AsBytes(ref result.Value));
        return result;
    }
}