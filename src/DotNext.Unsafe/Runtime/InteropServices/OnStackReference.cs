using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.InteropServices;

using CompilerServices;

/// <summary>
/// Represents a pointer to the stack memory.
/// </summary>
/// <remarks>
/// This type can be used to pass a pointer to the stack location through <see cref="OpaqueValue{T}"/>.
/// </remarks>
/// <typeparam name="T">The type of the value allocated on the stack.</typeparam>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct OnStackReference<T>: IPointer, ITypedReference<T>
    where T : struct, allows ref struct
{
    private readonly nint address;

    /// <summary>
    /// Initializes a new pointer to the stack memory.
    /// </summary>
    /// <param name="value">A location within the stack.</param>
    public OnStackReference(ref T value)
        => address = Unsafe.AddressOf(in value);

    nint IPointer.Address => address;

    /// <summary>
    /// Gets the referenced value.
    /// </summary>
    public unsafe ref T Value => ref Unsafe.AsRef<T>(address.ToPointer());

    /// <inheritdoc/>
    ref readonly T ITypedReference<T>.Value => ref Value;

    /// <inheritdoc/>
    public override string ToString() => address.ToString("X");
}