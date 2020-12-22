using System;
using System.Diagnostics;

namespace DotNext.Buffers
{
    public static partial class BufferHelpers
    {
        internal static int LinearGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize * ++chunkIndex, chunkSize);

        internal static int ExponentialGrowth(int chunkSize, ref int chunkIndex) => Math.Max(chunkSize << ++chunkIndex, chunkSize);

        internal static int NoGrowth(int chunkSize, ref int chunkIndex)
        {
            Debug.Assert(chunkIndex == 0);
            return chunkSize;
        }
    }
}