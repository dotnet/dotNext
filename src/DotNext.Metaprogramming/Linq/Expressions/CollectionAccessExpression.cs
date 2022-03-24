using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions;

using static Reflection.CollectionType;
using static Reflection.TypeExtensions;

/// <summary>
/// Represents access to the collection element using <see cref="ItemIndexExpression"/>.
/// </summary>
public sealed class CollectionAccessExpression : CustomExpression
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    private readonly PropertyInfo? indexer; // if null then collection is array
    private readonly PropertyInfo? count;   // if null then indexer != null because it has explicit Index parameter type

    /// <summary>
    /// Initializes a new collection access expression.
    /// </summary>
    /// <param name="collection">The expression representing collection.</param>
    /// <param name="index">The index of the element. Should be of type <see cref="System.Index"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="collection"/> doesn't provide implicit support of Index expression.</exception>
    /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges">Ranges and Indicies</seealso>
    public CollectionAccessExpression(Expression collection, Expression index)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(index);
        if (index.Type != typeof(Index))
            throw new ArgumentException(ExceptionMessages.TypeExpected<Index>(), nameof(index));
        var resolved = false;
        if (collection.Type.IsSZArray)
        {
            indexer = count = null;
            resolved = true;
        }
        else
        {
            foreach (var indexer in GetIndexers(collection.Type))
            {
                var parameters = indexer.GetIndexParameters();
                if (parameters.LongLength != 1L)
                    continue;
                var firstParam = parameters[0].ParameterType;
                if (firstParam == typeof(Index))
                {
                    count = null;
                    this.indexer = indexer;
                    resolved = true;
                    break;
                }

                if (firstParam == typeof(int))
                {
                    count = GetCountProperty(collection.Type) ?? throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
                    this.indexer = indexer;
                    resolved = true;
                    break;
                }
            }
        }

        Index = resolved ? index : throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
        Collection = collection;
    }

    internal static PropertyInfo? GetCountProperty(Type collection)
    {
        var intType = typeof(int);
        foreach (var lookup in collection.GetBaseTypes(includeTopLevel: true, includeInterfaces: collection.IsInterface))
        {
            PropertyInfo? property = lookup.GetProperty("Length", PublicInstance);
            if (property?.PropertyType == intType)
                return property;
            property = lookup.GetProperty("Count", PublicInstance);
            if (property?.PropertyType == intType)
                return property;
        }

        return null;
    }

    private static IEnumerable<PropertyInfo> GetIndexers(Type collection)
    {
        foreach (var lookup in collection.GetBaseTypes(includeTopLevel: true, includeInterfaces: collection.IsInterface))
        {
            DefaultMemberAttribute? defaultMember = lookup.GetCustomAttribute<DefaultMemberAttribute>(true);
            if (defaultMember is null)
                continue;
            PropertyInfo? property = lookup.GetProperty(defaultMember.MemberName, PublicInstance);
            if (property is not null)
                yield return property;
        }
    }

    /// <summary>
    /// Gets the index of the collection element.
    /// </summary>
    /// <value>The index of the item.</value>
    public Expression Index { get; }

    /// <summary>
    /// Gets the collection.
    /// </summary>
    /// <value>The collection.</value>
    public Expression Collection { get; }

    /// <summary>
    /// Gets result type of asynchronous operation.
    /// </summary>
    public override Type Type
    {
        get
        {
            var result = indexer?.PropertyType ?? Collection.Type.GetItemType();
            Debug.Assert(result is not null);
            return result;
        }
    }

    private static Expression ArrayAccess(Expression array, Expression index)
        => ArrayIndex(array, ItemIndexExpression.GetOffset(index, ArrayLength(array)));

    private static Expression MakeIndex(Expression collection, PropertyInfo count, Expression index)
        => ItemIndexExpression.GetOffset(index, Property(collection, count));

    /// <summary>
    /// Translates this expression into predefined set of expressions
    /// using Lowering technique.
    /// </summary>
    /// <returns>Translated expression.</returns>
    public override Expression Reduce()
    {
        ParameterExpression? temp = Collection is ParameterExpression ? null : Variable(Collection.Type);
        Expression result;
        if (indexer is null)
            result = ArrayAccess(temp ?? Collection, Index);
        else if (count is null)
            result = MakeIndex(temp ?? Collection, indexer, new[] { Index.Reduce() });
        else
            result = MakeIndex(temp ?? Collection, indexer, new[] { MakeIndex(temp ?? Collection, count, Index) });

        return temp is null ? result : Block(Type, new[] { temp }, Assign(temp, Collection), result);
    }

    /// <summary>
    /// Visit children expressions.
    /// </summary>
    /// <param name="visitor">Expression visitor.</param>
    /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
    protected override CollectionAccessExpression VisitChildren(ExpressionVisitor visitor)
    {
        var index = visitor.Visit(Index);
        var collection = visitor.Visit(Collection);
        return ReferenceEquals(index, Index) && ReferenceEquals(collection, Collection) ? this : new(collection, index);
    }
}