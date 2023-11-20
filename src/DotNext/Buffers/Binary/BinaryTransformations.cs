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
}