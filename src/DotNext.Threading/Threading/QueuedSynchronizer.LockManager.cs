namespace DotNext.Threading;

partial class QueuedSynchronizer
{
    private protected interface ILockManager
    {
        bool IsLockAllowed { get; }

        void AcquireLock();

        static virtual bool RequiresEmptyQueue => true;
    }
}