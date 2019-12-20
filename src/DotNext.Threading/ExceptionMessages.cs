using System.Diagnostics.CodeAnalysis;
using System.Resources;
using Assembly = System.Reflection.Assembly;

namespace DotNext
{
    [SuppressMessage("Globalization", "CA1304", Justification = "This is culture-specific resource strings")]
    [SuppressMessage("Globalization", "CA1305", Justification = "This is culture-specific resource strings")]
    [ExcludeFromCodeCoverage]
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager Resources = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string CollectionIsEmpty => Resources.GetString("CollectionIsEmpty");

        internal static string NotInWriteLock => Resources.GetString("NotInWriteLock");

        internal static string NotInReadLock => Resources.GetString("NotInReadLock");

        internal static string NotInUpgradeableReadLock => Resources.GetString("NotInUpgradeableReadLock");

        internal static string TokenNotCancelable => Resources.GetString("TokenNotCancelable");

        internal static string UnsupportedLockAcquisition => Resources.GetString("UnsupportedLockAcquisition");

        internal static string EmptyValueDelegate => Resources.GetString("EmptyValueDelegate");
    }
}