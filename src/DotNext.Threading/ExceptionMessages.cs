using Assembly = System.Reflection.Assembly;
using System.Diagnostics.CodeAnalysis;
using System.Resources;

namespace DotNext.Threading
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string CollectionIsEmpty => Resources.GetString("CollectionIsEmpty");

        internal static string NotInWriteLock => Resources.GetString("NotInWriteLock");

        internal static string NotInReadLock => Resources.GetString("NotInReadLock");

        internal static string NotInUpgradeableReadLock => Resources.GetString("NotInUpgradeableReadLock");
    }
}