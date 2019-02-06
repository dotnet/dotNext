using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext.Runtime.CompilerServices
{
    /// <summary>
    /// Represents value tuple builder with arbitrary number of tuple
    /// items.
    /// </summary>
    /// <see cref="ValueTuple"/>
    public sealed class ValueTupleBuilder: Disposable, IEnumerable<Type>
    {
        private readonly IList<Type> items = new List<Type>(7);//no more than 7 items
        private ValueTupleBuilder Rest;

        /// <summary>
        /// Number of elements in the tuple.
        /// </summary>
        public int Count => items.Count + (Rest is null ? 0 : Rest.Count);

        /// <summary>
        /// Constructs value tuple.
        /// </summary>
        /// <returns>Value tuple.</returns>
        public Type Build()
        {
            switch (Count)
            {
                case 0:
                    return typeof(ValueTuple);
                case 1:
                    return typeof(ValueTuple<>).MakeGenericType(items[0]);
                case 2:
                    return typeof(ValueTuple<,>).MakeGenericType(items[0], items[1]);
                case 3:
                    return typeof(ValueTuple<,,>).MakeGenericType(items[0], items[1], items[2]);
                case 4:
                    return typeof(ValueTuple<,,,>).MakeGenericType(items[0], items[1], items[2], items[3]);
                case 5:
                    return typeof(ValueTuple<,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4]);
                case 6:
                    return typeof(ValueTuple<,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5]);
                case 7:
                    return typeof(ValueTuple<,,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5], items[6]);
                default:
                    return typeof(ValueTuple<,,,,,,,>).MakeGenericType(items[0], items[1], items[2], items[3], items[4], items[5], items[6], Rest.Build());
            }
        }

        private void Build(Expression instance, Span<MemberExpression> output)
        {
            for (var i = 0; i < items.Count; i++)
                output[i] = Expression.Field(instance, "Item" + (i + 1));
            if (!(Rest is null))
            {
                instance = Expression.Field(instance, "Rest");
                Build(instance, output.Slice(8));
            }
        }

        public MemberExpression[] Build<E>(Func<Type, E> expressionFactory, out E expression)
            where E : Expression
        {
            expression = expressionFactory(Build());
            var fieldAccessExpression = new MemberExpression[Count];
            Build(expression, fieldAccessExpression.AsSpan());
            return fieldAccessExpression;
        }

        /// <summary>
        /// Adds new item into tuple.
        /// </summary>
        /// <param name="itemType">Type of item.</param>
        public void Add(Type itemType)
        {
            if (Count < 7)
                items.Add(itemType);
            else if (Rest is null)
                Rest = new ValueTupleBuilder() { itemType };
            else
                Rest.Add(itemType);
        }

        public void Add<T>() => Add(typeof(T));

        public IEnumerator<Type> GetEnumerator()
            => (Rest is null ? items : Enumerable.Concat(items, Rest)).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                items.Clear();
                Rest?.Dispose(disposing);
            }
        }
    }
}
