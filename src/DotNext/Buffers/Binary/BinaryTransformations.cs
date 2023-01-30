using System.Runtime.Intrinsics;
using System.Runtime.Versioning;

namespace DotNext.Buffers.Binary;

/// <summary>
/// Provides various binary transformations.
/// </summary>
public static partial class BinaryTransformations
{
    [RequiresPreviewFeatures]
    private interface ITransformation<T>
        where T : unmanaged
    {
        public static abstract Vector128<T> LoadAsVector128(ReadOnlySpan<T> buffer);

        public static abstract void StoreAsVector128(Span<T> buffer, Vector128<T> items);

        public static abstract Vector256<T> LoadAsVector256(ReadOnlySpan<T> buffer);

        public static abstract void StoreAsVector256(Span<T> buffer, Vector256<T> items);
    }
}