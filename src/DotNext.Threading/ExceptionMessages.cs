using Assembly = System.Reflection.Assembly;
using System.Diagnostics.CodeAnalysis;
using System.Resources;

namespace DotNext.Threading
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string ReleasedLock => resourceManager.GetString("ReleasedLock");

        internal static string CollectionIsEmpty => resourceManager.GetString("CollectionIsEmpty");

        internal static string NotInWriteLock => resourceManager.GetString("NotInWriteLock");

        internal static string NotInReadLock => resourceManager.GetString("NotInReadLock");

        internal static string NotInUpgradeableReadLock => resourceManager.GetString("NotInUpgradeableReadLock");
    }
}