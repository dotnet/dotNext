using System.Numerics;
using System.Runtime.CompilerServices;
using static System.Globalization.CultureInfo;
using static InlineIL.IL;
using static InlineIL.IL.Emit;

namespace DotNext;

/// <summary>
/// Various extension methods for core data types.
/// </summary>
public static class BasicExtensions
{
    internal static bool IsNull(object? obj) => obj is null;

    internal static bool IsNotNull(object? obj) => obj is not null;

    internal static bool IsTypeOf<T>(object? obj) => obj is T;

    internal static TOutput Identity<TInput, TOutput>(TInput input)
        where TInput : TOutput
        => input;

    /// <summary>
    /// Provides ad-hoc approach to associate some data with the object
    /// without modification of it.
    /// </summary>
    /// <remarks>
    /// This method allows to associate arbitrary user data with any object.
    /// User data storage is not a part of object type declaration.
    /// Modification of user data doesn't cause modification of internal state of the object.
    /// The storage is associated with the object reference.
    /// Any user data are transient and can't be passed across process boundaries (i.e. serialization is not supported).
    /// </remarks>
    /// <typeparam name="T">The type of the object.</typeparam>
    /// <param name="obj">Target object.</param>
    /// <returns>User data storage.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static UserDataStorage GetUserData<T>(this T obj)
        where T : class
        => new(obj);

    /// <summary>
    /// Checks whether the specified object is equal to one
    /// of the specified objects.
    /// </summary>
    /// <remarks>
    /// This method uses <see cref="object.Equals(object, object)"/>
    /// to check equality between two objects.
    /// </remarks>
    /// <typeparam name="T">The type of object to compare.</typeparam>
    /// <param name="value">The object to compare with other.</param>
    /// <param name="candidates">Candidate objects.</param>
    /// <returns><see langword="true"/>, if <paramref name="value"/> is equal to one of <paramref name="candidates"/>.</returns>
    public static bool IsOneOf<T>(this T value, ReadOnlySpan<T> candidates)
    {
        foreach (var other in candidates)
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
                return true;
        }

        return false;
    }

    internal static bool IsContravariant(object? obj, Type type) => obj?.GetType().IsAssignableFrom(type) ?? false;

    /// <summary>
    /// Reinterprets object reference.
    /// </summary>
    /// <remarks>
    /// This method can be used to get access to the explicitly implemented
    /// interface methods.
    /// </remarks>
    /// <example>
    /// <code>
    /// MemoryManager&lt;T&gt; manager;
    /// manager.As&lt;IDisposable&gt;().Dispose();
    /// </code>
    /// </example>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="obj">The object reference to reinterpret.</param>
    /// <returns>The reinterpreted <paramref name="obj"/> reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T As<T>(this T obj)
        where T : class?
        => obj;

    internal static TOutput ChangeType<TInput, TOutput>(this TInput input)
        where TInput : struct, IConvertible
        where TOutput : struct, IConvertible
        => (TOutput)input.ToType(typeof(TOutput), InvariantCulture);

    /// <summary>
    /// Attempts to get value from nullable container.
    /// </summary>
    /// <typeparam name="T">The underlying value type of the nullable type.</typeparam>
    /// <param name="nullable">Nullable value.</param>
    /// <param name="value">Underlying value.</param>
    /// <returns><see langword="true"/> if <paramref name="nullable"/> is not <see langword="null"/>; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetValue<T>(this T? nullable, out T value)
        where T : struct
    {
        value = nullable.GetValueOrDefault();
        return nullable.HasValue;
    }

    /// <summary>
    /// Converts <see cref="bool"/> into <see cref="int"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see cref="int"/> representation of <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(this bool value)
    {
        Push(value);
        return Return<int>();
    }

    /// <summary>
    /// Converts <see cref="bool"/> into <see cref="byte"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see cref="byte"/> representation of <paramref name="value"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte ToByte(this bool value)
    {
        Push(value);
        Conv_U1();
        return Return<byte>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sbyte ToSByte(this bool value)
    {
        Push(value);
        Conv_I1();
        return Return<sbyte>();
    }

    /// <summary>
    /// Converts <see cref="int"/> into <see cref="bool"/>.
    /// </summary>
    /// <param name="value">The value to convert.</param>
    /// <returns><see langword="true"/> if <c>value != 0</c>; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ToBoolean(this int value) => value is not 0;

    /// <summary>
    /// Normalizes value in the specified range.
    /// </summary>
    /// <typeparam name="TInput">The type of the input value and bounds.</typeparam>
    /// <typeparam name="TOutput">The type of normalized value.</typeparam>
    /// <param name="value">The value to be normalized. Must be in range [min..max].</param>
    /// <param name="min">The lower bound of the value.</param>
    /// <param name="max">The upper bound of the value.</param>
    /// <returns>The normalized value in range [-1..1] for signed value and [0..1] for unsigned value.</returns>
    public static TOutput Normalize<TInput, TOutput>(this TInput value, TInput min, TInput max)
        where TInput : struct, INumberBase<TInput>, IComparisonOperators<TInput, TInput, bool>
        where TOutput : struct, IFloatingPoint<TOutput>
    {
        var x = TOutput.CreateChecked(value);
        TInput y;
        if (value > TInput.Zero)
        {
            y = max;
        }
        else
        {
            y = min;
            x = -x;
        }

        return x / TOutput.CreateChecked(y);
    }

    /// <summary>
    /// Normalizes 64-bit unsigned integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    [CLSCompliant(false)]
    public static double Normalize(this ulong value)
    {
        const ulong fraction = ulong.MaxValue >> (64 - 53);
        const ulong exponent = 1UL << 53;
        return BitConverter.UInt64BitsToDouble(fraction & value) / BitConverter.UInt64BitsToDouble(exponent);
    }

    /// <summary>
    /// Normalizes 64-bit signed integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    public static double Normalize(this long value)
        => Normalize(unchecked((ulong)value));

    /// <summary>
    /// Normalizes 32-bit unsigned integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    [CLSCompliant(false)]
    public static float Normalize(this uint value)
    {
        const uint fraction = uint.MaxValue >> (32 - 24);
        const uint exponent = 1U << 24;
        return BitConverter.UInt32BitsToSingle(fraction & value) / BitConverter.UInt32BitsToSingle(exponent);
    }

    /// <summary>
    /// Normalizes 32-bit signed integer to interval [0..1).
    /// </summary>
    /// <param name="value">The value to be normalized.</param>
    /// <returns>The normalized value in range [0..1).</returns>
    public static float Normalize(this int value)
        => Normalize(unchecked((uint)value));
}