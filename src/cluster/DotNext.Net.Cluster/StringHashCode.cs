using System;
using static System.Globalization.CultureInfo;

namespace DotNext
{
    internal static class StringHashCode
    {
        internal static string BitwiseHashCodeAsHex(this string value)
            => value.AsSpan().BitwiseHashCode(false).ToString("X", InvariantCulture);
    }
}