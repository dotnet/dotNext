using System;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.DistributedServices
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct DistributedLock : IEquatable<DistributedLock>
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

        internal void Renew() => creationTime = DateTimeOffset.UtcNow;

        public bool Equals(DistributedLock other)
            => Owner == other.Owner && Version == other.Version && creationTime == other.creationTime;

        public override bool Equals(object other) => other is DistributedLock lockInfo && Equals(lockInfo);

        public override int GetHashCode() => HashCode.Combine(Owner, Version, creationTime);          
    }
}