using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using System;
using System.Reflection;
using System.Resources;

namespace DotNext.Net.Cluster.DistributedServices
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class LogMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.Net.Cluster.Consensus.Raft.LogMessages", Assembly.GetExecutingAssembly());
    
        internal static void ReleasingLock(this ILogger logger, string lockName)
            => logger.LogDebug(Resources.GetString("ReleasingLock"), lockName);
    
        internal static void ReleaseLockConfirm(this ILogger logger, string lockName)
            => logger.LogDebug(Resources.GetString("ReleaseLockConfirm"), lockName);
    
        internal static void FailedToUnlock(this ILogger logger, string lockName)
            => logger.LogError(Resources.GetString("FailedToUnlock"), lockName);
    
        internal static void AttemptsToAcquire(this ILogger logger, string lockName)
            => logger.LogDebug(Resources.GetString("AttemptToAcquire"), lockName);
    
        internal static void AcquireLockConfirm(this ILogger logger, string lockName)
            => logger.LogDebug(Resources.GetString("AcquireLockConfirm"), lockName);
    
        internal static void PendingLockConfirmation(this ILogger logger, string lockName)
            => logger.LogDebug(Resources.GetString("PendingLockConfirmation"), lockName);
    
        internal static void AcquireLockTimeout(this ILogger logger, string lockName)
            => logger.LogInformation(Resources.GetString("AcquireLockTimeout"), lockName);
    
        internal static void PendingLockCommit(this ILogger logger, string lockName)
            => logger.LogInformation(Resources.GetString("PendingLockCommit"), lockName);
    }
}
