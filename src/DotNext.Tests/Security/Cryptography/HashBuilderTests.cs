using System;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Xunit;

namespace DotNext.Security.Cryptography
{
    [ExcludeFromCodeCoverage]
    [Obsolete("This test collection is for backward compatibility only")]
    public sealed class HashBuilderTests : Test
    {
        [Fact]
        public static void HashBuilding()
        {
            byte[] data = { 1, 2, 3 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            using var builder = new HashBuilder(alg);
            False(builder.IsEmpty);
            builder.Add(data);
            Equal(alg.HashSize / 8, builder.Build(hash));
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static void NotEnoughHashLength()
        {
            using var builder = new HashBuilder("SHA-256");
            builder.Add(new byte[] { 1, 2, 3 });
            Throws<InvalidOperationException>(() => builder.Build(new byte[1]));
        }

        [Fact]
        public static void HashBuilding2()
        {
            byte[] data = { 1, 2, 3 };
            using var alg = new SHA256Managed();
            using var builder = new HashBuilder("SHA-256");
            var hash = new byte[builder.HashSize / 8];
            builder.Add(data);
            Equal(alg.HashSize / 8, builder.Build(hash));
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static void SequenceHash()
        {
            byte[] data = { 1, 2, 3, 5, 8, 13 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            using var builder = new HashBuilder(alg);
            builder.Add(ToReadOnlySequence<byte>(data, 3));
            Equal(alg.HashSize / 8, builder.Build(hash));
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static void PrimitiveValueHash()
        {
            var data = 20M;
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            using var builder = new HashBuilder(alg);
            builder.Add(data);
            Equal(alg.HashSize / 8, builder.Build(hash));
            builder.Reset();
            var hash2 = hash.Clone() as byte[];
            Array.Clear(hash2, 0, hash2.Length);
            NotEqual(hash, hash2);
            True(alg.TryComputeHash(Span.AsReadOnlyBytes(in data), hash2, out _));
            Equal(hash, hash2);
        }
    }
}