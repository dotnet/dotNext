using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Security.Cryptography
{
    [ExcludeFromCodeCoverage]
    [Obsolete("This test collection checks backward compatibility")]
    public sealed class HashAlgorithmTests : Test
    {
        [Fact]
        public static async Task HashEntirePipe()
        {
            byte[] data = { 1, 2, 3, 5, 8, 13 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            var pipe = new Pipe();
            ThreadPool.QueueUserWorkItem(async state =>
            {
                await pipe.Writer.WriteAsync(data);
                pipe.Writer.Complete();
            });
            await alg.ComputeHashAsync(pipe.Reader, hash);
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }

        [Fact]
        public static async Task HashPipe()
        {
            byte[] data = { 1, 2, 3, 5, 8, 13 };
            using var alg = new SHA256Managed();
            var hash = new byte[alg.HashSize / 8];
            var pipe = new Pipe();
            await pipe.Writer.WriteAsync(data);
            await alg.ComputeHashAsync(pipe.Reader, data.Length, hash);
            alg.Initialize();
            Equal(hash, alg.ComputeHash(data));
        }
    }
}