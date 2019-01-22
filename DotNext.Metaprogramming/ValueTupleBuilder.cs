using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace DotNext
{
    public sealed class ValueTupleBuilder: Disposable, IEnumerable<Type>
    {
        private readonly IList<Type> items = new List<Type>(7);//no more than 7 items
        private ValueTupleBuilder Rest;

        public int Count => items.Count + (Rest is null ? 0 : Rest.Count);

        public Type BuildType()
        {
            switch (Count)
            {
                case 0:
                    return typeof(ValueTuple);
                case 1:
                    return typeof(ValueTuple<>).MakeGenericType(items[0]);
                case 2:
                    return typeof(ValueTuple<,>).MakeGenericType(items[1], items[2]);
                case 3:
                    return typeof(ValueTuple<,,>).MakeGenericType(items[1], items[2], items[3]);
                case 4:
                    return typeof(ValueTuple<,,,>).MakeGenericType(items[1], items[2], items[3], items[4]);
                case 5:
                    return typeof(ValueTuple<,,,,>).MakeGenericType(items[1], items[2], items[3], items[4], items[5]);
                case 6:
                    return typeof(ValueTuple<,,,,,>).MakeGenericType(items[1], items[2], items[3], items[4], items[5], items[6]);
                case 7:
                    return typeof(ValueTuple<,,,,,,>).MakeGenericType(items[1], items[2], items[3], items[4], items[5], items[6], items[7]);
                default:
                    return typeof(ValueTuple<,,,,,,,>).MakeGenericType(items[1], items[2], items[3], items[4], items[5], items[6], items[7], Rest.BuildType());
            }
        }

        private void FillFields(Expression instance, Span<MemberExpression> output)
        {
            for (var i = 0; i < items.Count; i++)
                output[i] = Expression.Field(instance, "Item" + i);
            if (!(Rest is null))
            {
                instance = Expression.Field(instance, "Rest");
                FillFields(instance, output.Slice(8));
            }
        }

        public MemberExpression[] BuildFields<E>(Func<Type, E> expressionFactory, out E expression)
            where E : Expression
        {
            expression = expressionFactory(BuildType());
            var fieldAccessExpression = new MemberExpression[Count];
            FillFields(expression, fieldAccessExpression.AsSpan());
            return fieldAccessExpression;
        }

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
