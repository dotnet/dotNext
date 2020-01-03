using System;
using System.Runtime.InteropServices;

namespace DotNext.Threading
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DistributedLockInfo : IEquatable<DistributedLockInfo>
    {
        private DateTimeOffset creationTime;

        internal Guid Owner;

        //it is needed to distinguish different versions of the same lock
        internal Guid Version;  
         
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

        public bool Equals(DistributedLockInfo other)
            => Owner == other.Owner && Version == other.Version && creationTime == other.creationTime;

        public override bool Equals(object other) => other is DistributedLockInfo lockInfo && Equals(lockInfo);

        public override int GetHashCode() => HashCode.Combine(Owner, Version, creationTime);          
    }
}