using System;
using System.Resources;
using System.Reflection;

namespace DotNext
{
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string OptionalNoValue => resourceManager.GetString("OptionalNoValue");
        
        internal static string ReleasedLock => resourceManager.GetString("ReleasedLock");
        
        internal static string StreamNotReadable => resourceManager.GetString("StreamNotReadable");
        
        internal static string StreamNotWritable => resourceManager.GetString("StreamNotWritable");

        internal static string InvalidUserDataSlot => resourceManager.GetString("InvalidUserDataSlot");

        internal static string ConcreteDelegateExpected => resourceManager.GetString("ConcreteDelegateExpected");
    }
}