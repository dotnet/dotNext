using System;
using System.Threading.Tasks;

namespace Cheats.Threading.Tasks
{
    
    public static class TaskConverter
    {
        #region ToUInt64
        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<byte> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<sbyte> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<short> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<ushort> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<int> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<uint> task) => Convert.ToUInt64(await task);

        [CLSCompliant(false)]
        public static async Task<ulong> ToUInt64(this Task<long> task) => Convert.ToUInt64(await task);

        #endregion

        #region ToInt64
        public static async Task<long> ToInt64(this Task<byte> task) => Convert.ToInt64(await task);
        
        [CLSCompliant(false)]
        public static async Task<long> ToInt64(this Task<sbyte> task) => Convert.ToInt64(await task);

        public static async Task<long> ToInt64(this Task<short> task) => Convert.ToInt64(await task);

        [CLSCompliant(false)]
        public static async Task<long> ToInt64(this Task<ushort> task) => Convert.ToInt64(await task);

        public static async Task<long> ToInt64(this Task<int> task) => Convert.ToInt64(await task);

        [CLSCompliant(false)]
        public static async Task<long> ToInt64(this Task<uint> task) => Convert.ToInt64(await task);

        [CLSCompliant(false)]
        public static async Task<long> ToInt64(this Task<ulong> task) => Convert.ToInt64(await task);
        #endregion

        #region ToUInt32
        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<byte> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<sbyte> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<short> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<ushort> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<int> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<long> task) => Convert.ToUInt32(await task);

        [CLSCompliant(false)]
        public static async Task<uint> ToUInt32(this Task<ulong> task) => Convert.ToUInt32(await task);
        #endregion

        #region ToInt32
        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<byte> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<sbyte> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<short> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<ushort> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<uint> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<long> task) => Convert.ToInt32(await task);

        [CLSCompliant(false)]
        public static async Task<int> ToInt32(this Task<ulong> task) => Convert.ToInt32(await task);
        #endregion
    }
}