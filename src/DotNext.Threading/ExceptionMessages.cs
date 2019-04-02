using System.Resources;
using System.Reflection;

namespace DotNext.Threading
{
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string ReleasedLock => resourceManager.GetString("ReleasedLock");

        internal static string CollectionIsEmpty => resourceManager.GetString("CollectionIsEmpty");
    }
}