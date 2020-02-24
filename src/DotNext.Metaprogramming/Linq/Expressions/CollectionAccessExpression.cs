using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace DotNext.Linq.Expressions
{
    /// <summary>
    /// Represents access to the collection element using <see cref="ItemIndexExpression"/>.
    /// </summary>
    public sealed class CollectionAccessExpression : Expression
    {
        private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

        private readonly PropertyInfo? indexer; //if null then collection is array
        private readonly PropertyInfo? count;   //if null then indexer != null because it has explicit Index parameter type

        /// <summary>
        /// Initializes a new collection access expression.
        /// </summary>
        /// <param name="collection">The expression representing collection.</param>
        /// <param name="index">The index of the element.</param>
        /// <exception cref="ArgumentException"><paramref name="collection"/> doesn't provide implicit support of Index expression.</exception>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-8.0/ranges">Ranges and Indicies</seealso>
        public CollectionAccessExpression(Expression collection, ItemIndexExpression index)
        {
            if(collection is null)
                throw new ArgumentNullException(nameof(collection));
            if(index is null)
                throw new ArgumentNullException(nameof(index));
            var resolved = false;
            if(collection.Type.IsArray)
            {
                indexer = count = null;
                resolved = true;
            }
            else
                foreach(var indexer in GetIndexers(collection.Type))
                {
                    var parameters = indexer.GetIndexParameters();
                    if(parameters.LongLength != 1L)
                        continue;
                    var firstParam = parameters[0].ParameterType;
                    if(firstParam == typeof(Index))
                    {
                        count = null;
                        this.indexer = indexer;
                        resolved = true;
                        break;
                    }
                    if(firstParam == typeof(int))
                    {
                        count = GetCountProperty(collection.Type) ?? throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
                        this.indexer = indexer;
                        resolved = true;
                        break;
                    }
                }
            Index = resolved ? index : throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
            Collection = collection;
        }

        internal static PropertyInfo? GetCountProperty(Type collection)
        {
            PropertyInfo? property = collection.GetProperty("Length", PublicInstance | BindingFlags.FlattenHierarchy);
            var intType = typeof(int);
            if(property?.PropertyType == intType)
                return property;
            property = collection.GetProperty("Count", PublicInstance | BindingFlags.FlattenHierarchy);
            return property?.PropertyType == intType ? property : null;
        }

        private static IEnumerable<PropertyInfo> GetIndexers(Type collection)
        {
            DefaultMemberAttribute? defaultMember = collection.GetCustomAttribute<DefaultMemberAttribute>(true);
            if(defaultMember != null)
                foreach(var member in collection.FindMembers(MemberTypes.Property, PublicInstance, Type.FilterName, defaultMember.MemberName))
                    if(member is PropertyInfo property)
                        yield return property;
        }

        /// <summary>
        /// Gets the index of the collection element.
        /// </summary>
        /// <value>The index of the item.</value>
        public ItemIndexExpression Index { get; }

        /// <summary>
        /// Gets the collection.
        /// </summary>
        /// <value>The collection.</value>
        public Expression Collection { get; }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => indexer?.PropertyType ?? Collection.Type.GetElementType();

        /// <summary>
        /// Always return <see langword="true"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Gets expression node type.
        /// </summary>
        /// <see cref="ExpressionType.Extension"/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        private static BinaryExpression ArrayAccess(Expression array, ItemIndexExpression index)
            => ArrayIndex(array, index.IsFromEnd ?
                Call(index, nameof(System.Index.GetOffset), null, ArrayLength(array)) :
                index.Value);

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            if(indexer is null)
                return ArrayAccess(Collection, Index);
            if(count is null)
                return Expression.MakeIndex(Collection, indexer, new []{ Index });
            var indexValue = Index.IsFromEnd ?
                Call(Index, nameof(System.Index.GetOffset), null, Property(Collection, count)) :
                Index.Value;
            
            return Expression.MakeIndex(Collection, indexer, new [] { indexValue });
        }
    }
}