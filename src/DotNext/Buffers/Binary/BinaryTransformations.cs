namespace DotNext.Buffers.Binary;

/// <summary>
/// Provides various binary transformations.
/// </summary>
public static partial class BinaryTransformations
{
    private interface IUnaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T value);
    }

    private interface IBinaryTransformation<T>
        where T : unmanaged
    {
        public static abstract T Transform(T x, T y);
    }

    /// <summary>
    /// Reverse bytes in the specified value of blittable type.
    /// </summary>
    /// <typeparam name="T">Blittable type.</typeparam>
    /// <param name="value">The value which bytes should be reversed.</param>
    public static void Reverse<T>(ref T value)
        where T : unmanaged
        => Span.AsBytes(ref value).Reverse();
}