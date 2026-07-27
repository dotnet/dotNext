using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

namespace DotNext;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());
    
    private static string GetResourceString([CallerMemberName] string callerName = "")
        => Resources.GetString(callerName)!;

    internal static string OptionalNoValue => GetResourceString();

    internal static string OptionalNullValue => GetResourceString();

    internal static string InvalidUserDataSlot => GetResourceString();

    internal static string CastNullToValueType => GetResourceString();

    internal static string UnsupportedLockAcquisition => GetResourceString();

    internal static string ConcreteDelegateExpected => GetResourceString();

    internal static string InvalidExpressionTree => GetResourceString();

    internal static string NotEnoughMemory => GetResourceString();

    internal static string BoxedValueTypeExpected<T>()
        where T : struct
        => string.Format(GetResourceString(), typeof(T));

    internal static string ResourceEntryIsNull(string name)
        => string.Format(GetResourceString(), name);

    internal static string LargeBuffer => GetResourceString();

    internal static string MalformedBase64 => GetResourceString();

    internal static string UndefinedValueDetected => GetResourceString();

    internal static string KeyAlreadyExists => GetResourceString();

    internal static string NoResult<TError>(TError errorCode)
        where TError : struct, Enum
        => string.Format(GetResourceString(), errorCode);

    internal static string EndOfBuffer(long remaining) => string.Format(GetResourceString(), remaining);

    internal static string OverlappedRange => GetResourceString();

    internal static string FullyQualifiedPathExpected => GetResourceString();

    internal static string BufferTooSmall => GetResourceString();

    internal static string EmptyCollection => GetResourceString();

    internal static string BufferSizeLimitExceeded => GetResourceString();
}