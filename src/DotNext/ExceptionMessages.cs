using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;

namespace DotNext;

[ExcludeFromCodeCoverage]
internal static class ExceptionMessages
{
    private static readonly ResourceManager Resources = new("DotNext.ExceptionMessages", Assembly.GetExecutingAssembly());

    internal static string OptionalNoValue => Resources.GetString("OptionalNoValue")!;

    internal static string OptionalNullValue => Resources.GetString("OptionalNullValue")!;

    internal static string InvalidUserDataSlot => Resources.GetString("InvalidUserDataSlot")!;

    internal static string CastNullToValueType => Resources.GetString("CastNullToValueType")!;

    internal static string UnsupportedLockAcquisition => Resources.GetString("UnsupportedLockAcquisition")!;

    internal static string ConcreteDelegateExpected => Resources.GetString("ConcreteDelegateExpected")!;

    internal static string InvalidExpressionTree => Resources.GetString("InvalidExpressionTree")!;

    internal static string NotEnoughMemory => Resources.GetString("NotEnoughMemory")!;

    internal static string BoxedValueTypeExpected<T>()
        where T : struct
        => string.Format(Resources.GetString("BoxedValueTypeExpected")!, typeof(T));

    internal static string ResourceEntryIsNull(string name)
        => string.Format(Resources.GetString("ResourceEntryIsNull")!, name);

    internal static string LargeBuffer => Resources.GetString("LargeBuffer")!;

    internal static string MalformedBase64 => Resources.GetString("MalformedBase64")!;

    internal static string UndefinedValueDetected => Resources.GetString("UndefinedValueDetected")!;

    internal static string InvalidHexInput(char ch)
        => string.Format(Resources.GetString("InvalidHexInput")!, ch);

    internal static string KeyAlreadyExists => Resources.GetString("KeyAlreadyExists")!;

    internal static string NoResult<TError>(TError errorCode)
        where TError : struct, Enum
        => string.Format(Resources.GetString("NoResult")!, errorCode);

    internal static string EndOfBuffer(long remaining) => string.Format(Resources.GetString("EndOfBuffer")!, remaining);

    internal static string OverlappedRange => Resources.GetString("OverlappedRange")!;

    internal static string FullyQualifiedPathExpected => Resources.GetString("FullyQualifiedPathExpected")!;
}