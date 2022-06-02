using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Messaging.Gossip
{
    using Buffers;

    [ExcludeFromCodeCoverage]
    public sealed class PeerTransientIdTests : Test
    {
        [Fact]
        public static void Equality()
        {
            var id1 = new PeerTransientId(Random.Shared);
            var id2 = new PeerTransientId(Random.Shared);
            NotEqual(id1, id2);
            False(id1 == id2);
            True(id1 != id2);
            False(id1.Equals(id2));
            id2 = id1;
            Equal(id1, id2);
            True(id1 == id2);
            False(id1 != id2);
            True(id1.Equals(id2));
        }

        [Fact]
        public static unsafe void RestoreFromBytes()
        {
            var id1 = new PeerTransientId(Random.Shared);
            Span<byte> bytes = stackalloc byte[PeerTransientId.Size];
            var writer = new SpanWriter<byte>(bytes);
            id1.Format(ref writer);

            var id2 = new PeerTransientId(bytes);
            Equal(id1, id2);
        }

        [Fact]
        public static void Parsing()
        {
            var expected = new PeerTransientId(Random.Shared);
            True(PeerTransientId.TryParse(expected.ToString().AsSpan(), out var actual));
            Equal(expected, actual);
            var invalidHex = "AB142244";
            False(PeerTransientId.TryParse(invalidHex.AsSpan(), out _));
        }
    }
}