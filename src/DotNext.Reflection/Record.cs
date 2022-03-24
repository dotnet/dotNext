using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DotNext;

using Reflection;
using Runtime.CompilerServices;

/// <summary>
/// Represents a concept that describes a record class.
/// </summary>
/// <typeparam name="T">The candidate type.</typeparam>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record">Records (C# Reference)</seealso>
[CLSCompliant(false)]
[Concept]
public static class Record<T>
    where T : class
{
    private const string CloneMethodName = "<Clone>$";

    private static readonly Func<T, T> CloneMethod;

    static Record()
    {
        CloneMethod = Type<T>.Method.Require<T>(CloneMethodName);
    }

    /// <summary>
    /// Creates a delegate that can be used to create a fresh copy of the original record.
    /// </summary>
    /// <param name="record">A record of reference type.</param>
    /// <returns>A factory that can produce fresh copies.</returns>
    public static Func<T> Bind(T record)
        => CloneMethod.Method.CreateDelegate<Func<T>>(record);

    /// <summary>
    /// Creates a copy of the record.
    /// </summary>
    /// <param name="record">A record of reference type.</param>
    /// <returns>A copy of the record.</returns>
    public static T Clone(T record) => CloneMethod(record);
}