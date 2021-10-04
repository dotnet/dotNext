namespace DotNext;

/// <summary>
/// Represents parsing logic.
/// </summary>
/// <typeparam name="T">The type of the result.</typeparam>
/// <param name="input">A set of characters representing formatted value of type <typeparamref name="T"/>.</param>
/// <param name="provider">The format provider.</param>
/// <returns>The parsed value.</returns>
/// <exception cref="FormatException">The string is in wrong format.</exception>
public delegate T Parser<out T>(ReadOnlySpan<char> input, IFormatProvider? provider)
    where T : notnull;