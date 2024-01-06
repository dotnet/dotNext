using System.Numerics;

namespace DotNext.IO.Hashing;

using System.Runtime.InteropServices;
using Collections.Generic;

public sealed class FNV1aTests : Test
{
    private static void HashTest<THash, TParameters>(FNV1a<THash, TParameters> algorithm)
        where THash : unmanaged, IBinaryNumber<THash>
        where TParameters : notnull, IFNV1aParameters<THash>
    {
        ReadOnlySpan<byte> chunk1 = [1, 2, 3, 4, 5];
        ReadOnlySpan<byte> chunk2 = [6, 7, 8, 9, 10];
        ReadOnlySpan<byte> data = [.. chunk1, .. chunk2];

        algorithm.Append(chunk1);
        algorithm.Append(chunk2);
        var hash = algorithm.GetCurrentHash();

        algorithm.Reset();
        algorithm.Append(data);
        Equal(algorithm.GetCurrentHash(), hash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Hash32(bool salted) => HashTest(new FNV1a32(salted));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Hash64(bool salted) => HashTest(new FNV1a64(salted));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void Hash128(bool salted) => HashTest(new FNV1a64(salted));

    [Fact]
    public static void HashList()
    {
        List<int> list = [1, 2, 3, 4];
        var hash1 = FNV1a32.Hash<List<int>, int>(List.Indexer<int>.Getter, list.Count, list);
        var hash2 = FNV1a32.Hash<int>(CollectionsMarshal.AsSpan(list));
        var hash3 = FNV1a32.Hash<uint>(MemoryMarshal.Cast<int, uint>(CollectionsMarshal.AsSpan(list)));

        Equal(hash1, hash2);
        Equal(hash1, hash3);
    }

    [Fact]
    public static void HashGuid()
    {
        Span<Guid> elements = stackalloc Guid[3];
        Random.Shared.GetItems(elements);

        var algorithm = new FNV1a32();
        algorithm.Append<Guid>(elements);
        var hash1 = algorithm.GetCurrentHash();

        Equal(FNV1a32.Hash<Guid>(elements), algorithm.GetCurrentHash());
    }
}