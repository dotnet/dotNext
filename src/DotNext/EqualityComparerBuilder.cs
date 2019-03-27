using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

namespace DotNext
{
    using Reflection;

    public sealed class SpecialEqualityComparer<T>: IEqualityComparer<T>
    {
        public static readonly IEqualityComparer<T> Instance;

        static SpecialEqualityComparer()
        {
            var type = typeof(T);
            switch(type.GetTypeCode())
            {
                case TypeCode.Empty:
                    Instance = new SpecialEqualityComparer<T>();
                    return;
                default:    //for primitive types use default equality comparer
                    Instance = EqualityComparer<T>.Default;
                    return;
                case TypeCode.Object:
                    Func<T, int> hashCode;
                    Func<T, T, bool> comparer;
                    if(type.IsValueType)
                    {
                        var valueType = typeof(ValueType<>).MakeGenericType(type);
                        var x = Expression.Parameter(type);
                        var y = Expression.Parameter(type);
                        //call bitwise hash code
                        hashCode = Expression.Lambda<Func<T, int>>(
                            Expression.Call(null, valueType.GetMethod(nameof(ValueType<int>.BitwiseHashCode), new[] { type, typeof(bool) }), x, Expression.Constant(true, typeof(bool))),
                            true,
                            x
                        ).Compile();
                        //call bitwise equality
                        
                    }
                    Instance = new SpecialEqualityComparer<T>(hashCode, comparer);
                    return;
            }
        }

        private readonly Func<T, int> hashCode;
        private readonly Func<T, T, bool> comparer;

        private SpecialEqualityComparer() : this(null, null) { }

        private SpecialEqualityComparer(Func<T, int> hashCode, Func<T, T, bool> comparer)
        {
            this.hashCode = hashCode;
            this.comparer = comparer;
        }

        int IEqualityComparer<T>.GetHashCode(T obj) => hashCode is null ? 0 : hashCode(obj);
        bool IEqualityComparer<T>.Equals(T x, T y) => comparer is null || comparer(x, y);
    }
}