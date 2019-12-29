using System;
using System.Runtime.InteropServices;

namespace DotNext.Threading
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DistributedLockInfo
    {
        private DateTimeOffset creationTime;

        internal Guid Owner;
        internal DateTimeOffset CreationTime
        {
            get => creationTime;
            set => creationTime = value.ToUniversalTime();
        }

        internal TimeSpan LeaseTime;

        internal bool IsExpired
        {
            get
            {
                var currentTime = DateTimeOffset.Now.ToUniversalTime();
                return CreationTime + LeaseTime <= currentTime;
            }
        }           
    }
}