using System;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        internal static int LinearGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize * ++chunkIndex, chunkSize);

        internal static int ExponentialGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize << ++chunkIndex, chunkSize);

        internal static int NoGrowth(int chunkSize, ref int chunkIndex) => chunkSize;
    }
}