using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Debug = System.Diagnostics.Debug;
using RuntimeHelpers = System.Runtime.CompilerServices.RuntimeHelpers;

namespace DotNext.Linq.Expressions
{
    using static Reflection.TypeExtensions;

    /// <summary>
    /// Represents slice of collection using range.
    /// </summary>
    public sealed class SliceExpression : Expression
    {
        private readonly MethodInfo? slice; //if null then array
        private readonly PropertyInfo? count;   //if null then object supports Slice method with Range parameter

        /// <summary>
        /// Initializes a new slice of collection or array.
        /// </summary>
        /// <param name="collection">The collection or array.</param>
        /// <param name="range">The requested range of collection or array.</param>
        /// <exception cref="ArgumentException"><paramref name="collection"/> doesn't implement <c>Slice</c> method, <c>Length</c> or <c>Count</c> property.</exception>
        public SliceExpression(Expression collection, RangeExpression range)
        {
            if (collection is null)
                throw new ArgumentNullException(nameof(collection));
            if (range is null)
                throw new ArgumentNullException(nameof(range));
            var resolved = false;
            if (collection.Type.IsSingleDimensionalArray())
            {
                slice = null;
                count = null;
                resolved = true;
            }
            else if(collection.Type == typeof(string))
            {
                slice = new Func<string, Range, string>(StringExtensions.Substring).Method;
                count = null;
                resolved = true;
            }
            else
                foreach (var slice in GetSliceMethods(collection.Type))
                {
                    var parameters = slice.GetParameters();
                    if (parameters.LongLength == 1L && parameters[0].ParameterType == typeof(Range))
                    {
                        count = null;
                        this.slice = slice;
                        resolved = true;
                        break;
                    }
                    var intType = typeof(int);
                    if (parameters.LongLength == 2L && parameters[0].ParameterType == intType && parameters[1].ParameterType == intType)
                    {
                        count = CollectionAccessExpression.GetCountProperty(collection.Type) ?? throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
                        this.slice = slice;
                        resolved = true;
                        break;
                    }
                }
            Range = resolved ? range : throw new ArgumentException(ExceptionMessages.CollectionExpected(collection.Type), nameof(collection));
            Collection = collection;
        }

        private static IEnumerable<MethodInfo> GetSliceMethods(Type collection)
        {
            foreach (var lookup in collection.GetBaseTypes(includeTopLevel: true, includeInterfaces: collection.IsInterface))
                foreach (var member in lookup.FindMembers(MemberTypes.Method, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, Type.FilterName, "Slice"))
                    if (member is MethodInfo method)
                        yield return method;
        }

        /// <summary>
        /// Gets result type of asynchronous operation.
        /// </summary>
        public override Type Type => slice?.ReturnType ?? Collection.Type;

        /// <summary>
        /// Always return <see langword="true"/>.
        /// </summary>
        public override bool CanReduce => true;

        /// <summary>
        /// Gets expression node type.
        /// </summary>
        /// <see cref="ExpressionType.Extension"/>
        public override ExpressionType NodeType => ExpressionType.Extension;

        /// <summary>
        /// Gets collection.
        /// </summary>
        public Expression Collection { get; }

        /// <summary>
        /// Gets slice range.
        /// </summary>
        public RangeExpression Range { get; }

        private static MethodCallExpression SubArray(Expression array, RangeExpression range)
        {
            MethodInfo? subArray = typeof(RuntimeHelpers).GetMethod(nameof(RuntimeHelpers.GetSubArray), 1, new[] { Type.MakeGenericMethodParameter(0).MakeArrayType(), typeof(Range) });
            Debug.Assert(!(subArray is null));
            subArray = subArray.MakeGenericMethod(array.Type.GetElementType());
            return Call(subArray, array, range.Reduce());
        }

        private static MethodCallExpression SubCollection(Expression collection, MethodInfo slice, PropertyInfo count, RangeExpression range)
        {
            var start = CollectionAccessExpression.MakeIndex(collection, count, range.Start);
            var length = Subtract(range.End.IsFromEnd ? Subtract(Property(collection, count), range.End.Value) : range.End.Value, start);
            return Call(collection, slice, start, length);
        }

        /// <summary>
        /// Translates this expression into predefined set of expressions
        /// using Lowering technique.
        /// </summary>
        /// <returns>Translated expression.</returns>
        public override Expression Reduce()
        {
            ParameterExpression? temp = Collection is ParameterExpression ? null : Variable(Collection.Type);
            Expression result;
            if (slice is null)
                result = SubArray(temp ?? Collection, Range);
            else if (count is null)
                result = slice.IsStatic ? Call(slice, temp ?? Collection, Range.Reduce()) : Call(temp ?? Collection, slice, Range.Reduce());
            else
                result = SubCollection(temp ?? Collection, slice, count, Range);

            return temp is null ? result : Block(Type, new[] { temp }, Assign(temp, Collection), result);
        }

        /// <summary>
        /// Visit children expressions.
        /// </summary>
        /// <param name="visitor">Expression visitor.</param>
        /// <returns>Potentially modified expression if one of children expressions is modified during visit.</returns>
        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            var range = (RangeExpression)visitor.Visit(Range);
            var collection = visitor.Visit(Collection);

            return ReferenceEquals(range, Range) && ReferenceEquals(collection, Collection) ? this : new SliceExpression(collection, range);
        }
    }
}
