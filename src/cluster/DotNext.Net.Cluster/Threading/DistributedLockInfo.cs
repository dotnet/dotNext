using System;
using System.Runtime.InteropServices;

namespace DotNext.Threading
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DistributedLockInfo
    {
        private DateTimeOffset creationTime;

        internal Guid Owner;

        //it is needed to distinguish different versions of the same lock
        internal Guid Id;  
         
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
                var currentTime = DateTimeOffset.UtcNow;
                return CreationTime + LeaseTime <= currentTime;
            }
        }           

        internal DistributedLockInfo Update(string name, DistributedLockInfo existing)
            => existing.IsExpired ? this : existing;
    }
}