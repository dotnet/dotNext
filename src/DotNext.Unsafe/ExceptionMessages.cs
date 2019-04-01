using System;
using System.Resources;
using System.Reflection;

namespace DotNext
{
    internal static class ExceptionMessages
    {
        private static readonly ResourceManager resourceManager = new ResourceManager("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

        internal static string NullSource => resourceManager.GetString("NullSource");

        internal static string NullDestination => resourceManager.GetString("NullDestination");

        internal static string WrongTargetTypeSize => resourceManager.GetString("WrongTargetTypeSize");

        internal static string NullPtr => resourceManager.GetString("NullPtr");

        internal static string HandleClosed => resourceManager.GetString("HandleClosed");

        internal static string ArrayNegativeLength => resourceManager.GetString("ArrayNegativeLength");

        internal static string InvalidIndexValue(long length) => string.Format(resourceManager.GetString("InvalidIndexValue"), length);

        internal static string InvalidOffsetValue(long size) => string.Format(resourceManager.GetString("InvalidOffsetValue"), size);

        internal static string TargetSizeMustBeMultipleOf => resourceManager.GetString("TargetSizeMustBeMultipleOf");

        internal static string ExpectedType(Type t) => string.Format(resourceManager.GetString("ExpectedType"), t.FullName);

        internal static string StreamNotReadable => resourceManager.GetString("StreamNotReadable");

        internal static string StreamNotWritable => resourceManager.GetString("StreamNotWritable");
    }
}
