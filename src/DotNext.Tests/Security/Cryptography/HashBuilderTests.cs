using System.Security.Cryptography;
using Xunit;

namespace DotNext.Security.Cryptography
{
    public sealed class HashBuilderTests : Assert
    {
        [Fact]
        public static void HashBuilding()
        {
            byte[] data = {1, 2, 3};
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            using var builder = new HashBuilder(alg);
            builder.Add(data);
            Equal(alg.HashSize / 8, builder.Build(hash));
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static void HashBuilding2()
        {
            byte[] data = {1, 2, 3};
            using var alg = new SHA256Managed();
            using var builder = new HashBuilder("SHA-256");
            var hash = new byte[builder.HashSize / 8];
            builder.Add(data);
            Equal(alg.HashSize / 8, builder.Build(hash));
            Equal(hash, alg.ComputeHash(data));
        }
    }
}