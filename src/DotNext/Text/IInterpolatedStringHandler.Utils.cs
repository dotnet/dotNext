using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace DotNext.Text;

using Runtime.CompilerServices;

partial interface IInterpolatedStringHandler
{
    internal static string? ToStringNonBoxing<T>([DisallowNull] T value)
    {
        Debug.Assert(value is not null);

        if (typeof(T).IsEnum)
            return UnsafeEnumToString(value);

        if (typeof(T).IsValueType && RuntimeHelpers.ToStringRequiresBoxing<T>())
            return typeof(T).ToString();

        return value.ToString();
    }
    
    private static int Align<T>(Span<T> buffer, [ConstantExpected] T whitespace, int alignment, int elementsWritten, bool leftAlign)
        where T : unmanaged, IEquatable<T>
    {
        var padding = alignment - elementsWritten;

        if (padding <= 0)
        {
            alignment = elementsWritten;
        }
        else
        {
            Span<T> filler;
            if (leftAlign)
            {
                filler = buffer.Slice(elementsWritten, padding);
            }
            else
            {
                filler = buffer.TrimLength(padding, out var rest);
                buffer.Slice(0, elementsWritten).CopyTo(rest);
            }

            filler.Fill(whitespace);
        }

        return alignment;
    }
}