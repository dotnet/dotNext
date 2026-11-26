using System.Collections;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Runtime.CompilerServices;

using Collections.Generic;

/// <summary>
/// Represents value tuple builder with arbitrary number of tuple
/// items.
/// </summary>
/// <seealso cref="ValueTuple"/>
/// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/tuples">Tuples</seealso>
public sealed class ValueTupleBuilder : IEnumerable<Type>, IResettable
{
    // no more than 7 items because max number of generic arguments of tuple type
    private const int MaxTupleSize = 7;
    private InlineArray7<Type> items;
    private int count;
    private ValueTupleBuilder? rest;

    /// <summary>
    /// Number of elements in the tuple.
    /// </summary>
    public int Count => count + (rest?.Count ?? 0);

    /// <summary>
    /// Constructs value tuple.
    /// </summary>
    /// <returns>Value tuple.</returns>
    public Type Build() => Count switch
    {
        0 => typeof(ValueTuple),
        1 => typeof(ValueTuple<>).MakeGenericType(items[0]),
        2 => typeof(ValueTuple<,>).MakeGenericType(items[0], items[1]),
        3 => typeof(ValueTuple<,,>).MakeGenericType(items[0], items[1], items[2]),
        4 => typeof(ValueTuple<,,,>).MakeGenericType(items[0], items[1], items[2], items[3]),
        5 => typeof(ValueTuple<,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4]),
        6 => typeof(ValueTuple<,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5]),
        7 => typeof(ValueTuple<,,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5], items[6]),
        _ => typeof(ValueTuple<,,,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5], items[6], rest!.Build()),
    };

    private void Build(Expression instance, Span<MemberExpression> output)
    {
        for (var i = 0; i < count; i++)
            output[i] = Expression.Field(instance, "Item" + (i + 1));
        if (rest is not null)
        {
            instance = Expression.Field(instance, "Rest");
            rest.Build(instance, output.Slice(MaxTupleSize));
        }
    }

    /// <summary>
    /// Constructs expression tree based on value tuple type.
    /// </summary>
    /// <typeparam name="TExpression">Type of expression tree.</typeparam>
    /// <param name="expressionFactory">A function accepting value tuple type and returning expression tree.</param>
    /// <param name="expression">Constructed expression.</param>
    /// <returns>Sorted array of value tuple type components.</returns>
    public MemberExpression[] Build<TExpression>(Func<Type, TExpression> expressionFactory, out TExpression expression)
        where TExpression : Expression
    {
        expression = expressionFactory(Build());
        var fieldAccessExpression = new MemberExpression[Count];
        Build(expression, fieldAccessExpression.AsSpan());
        return fieldAccessExpression;
    }

    /// <summary>
    /// Adds a new component into tuple.
    /// </summary>
    /// <param name="itemType">The type of the tuple component.</param>
    public void Add(Type itemType)
    {
        if (count < MaxTupleSize)
            items[count++] = itemType;
        else if (rest is null)
            rest = [itemType];
        else
            rest.Add(itemType);
    }

    /// <summary>
    /// Adds a new component into tuple.
    /// </summary>
    /// <typeparam name="T">The type of the tuple component.</typeparam>
    public void Add<T>() => Add(typeof(T));

    /// <summary>
    /// Resets the builder.
    /// </summary>
    public void Reset()
    {
        count = 0;
        Span<Type> types = items;
        types.Clear();
        
        if (rest is not null)
        {
            rest.Reset();
            rest = null;
        }
    }

    /// <summary>
    /// Returns an enumerator over all tuple components.
    /// </summary>
    /// <returns>An enumerator over all tuple components.</returns>
    public Enumerator GetEnumerator() => new(this);

    private IEnumerator<Type> ToClassicEnumerator()
        => IEnumerator<Type>.Create(GetEnumerator());

    /// <inheritdoc/>
    IEnumerator<Type> IEnumerable<Type>.GetEnumerator() => ToClassicEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => ToClassicEnumerator();
    
    /// <summary>
    /// Represents enumerator over tuple items.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator : IEnumerator<Enumerator, Type>
    {
        private ValueTupleBuilder? builder;
        private int offset;

        internal Enumerator(ValueTupleBuilder builder)
        {
            this.builder = builder;
            offset = -1;
        }

        /// <inheritdoc/>
        public readonly Type Current => builder?.items[offset] ?? throw new InvalidOperationException();

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (builder is not null)
            {
                if (++offset < builder.count)
                    return true;

                if (builder.rest is { } tail)
                {
                    offset = 0;
                    builder = tail;
                    return true;
                }
            }

            return false;
        }
    }
}