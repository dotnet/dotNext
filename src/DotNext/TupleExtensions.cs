using System.Runtime.InteropServices;
using static System.Runtime.CompilerServices.Unsafe;

namespace DotNext;

using ITuple = System.Runtime.CompilerServices.ITuple;

/// <summary>
/// Provides extension methods for tuples.
/// </summary>
public static class TupleExtensions
{
    /// <summary>
    /// Copies tuple items to an array.
    /// </summary>
    /// <typeparam name="T">The type of the tuple.</typeparam>
    /// <param name="tuple">The tuple instance.</param>
    /// <returns>An array of tuple items.</returns>
    public static object?[] ToArray<T>(this T tuple)
        where T : notnull, ITuple
    {
        object?[] result;
        if (tuple.Length > 0)
        {
            result = new object?[tuple.Length];
            for (var i = 0; i < result.Length; i++)
                result[i] = tuple[i];
        }
        else
        {
            result = Array.Empty<object?>();
        }

        return result;
    }

    private static Span<T> TupleToSpan<T, TTuple>(ref TTuple tuple)
        where TTuple : struct, ITuple
        => MemoryMarshal.CreateSpan(ref As<TTuple, T>(ref tuple), tuple.Length);

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref ValueTuple tuple)
        => Span<T>.Empty;

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in ValueTuple tuple)
        => ReadOnlySpan<T>.Empty;

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref ValueTuple<T> tuple)
        => TupleToSpan<T, ValueTuple<T>>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in ValueTuple<T> tuple)
        => TupleToSpan<T, ValueTuple<T>>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T) tuple)
        => TupleToSpan<T, ValueTuple<T, T>>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T) tuple)
        => TupleToSpan<T, ValueTuple<T, T>>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T, T) tuple)
        => TupleToSpan<T, (T, T, T)>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T) tuple)
        => TupleToSpan<T, (T, T, T)>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T)>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T)>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T)>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T)>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T, T)>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T, T)>(ref AsRef(in tuple));

    /// <summary>
    /// Obtains a span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static Span<T> AsSpan<T>(this ref (T, T, T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T, T, T)>(ref tuple);

    /// <summary>
    /// Obtains read-only span over tuple items.
    /// </summary>
    /// <param name="tuple">The tuple.</param>
    /// <typeparam name="T">The type of items in the tuple.</typeparam>
    /// <returns>The span over items in the tuple.</returns>
    public static ReadOnlySpan<T> AsReadOnlySpan<T>(this in (T, T, T, T, T, T, T) tuple)
        => TupleToSpan<T, (T, T, T, T, T, T, T)>(ref AsRef(in tuple));
}