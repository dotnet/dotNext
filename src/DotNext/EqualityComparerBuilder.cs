using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

namespace DotNext
{
    using Reflection;
    using Collections.Generic;

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
                    var x = Expression.Parameter(type);
                    var y = Expression.Parameter(type);
                    //for value type use bitwise operations
                    if(type.IsValueType)
                    {
                        var valueType = typeof(ValueType<>).MakeGenericType(type);
                        //call bitwise hash code
                        hashCode = Expression.Lambda<Func<T, int>>(
                            Expression.Call(null, valueType.GetMethod(nameof(ValueType<int>.BitwiseHashCode), new[] { type, typeof(bool) }), x, Expression.Constant(true, typeof(bool))),
                            true,
                            x
                        ).Compile();
                        //call bitwise equality
                        comparer = valueType.GetMethod(nameof(ValueType<int>.BitwiseEquals), new[] { type, type }).CreateDelegate<Func<T, T, bool>>();
                    }
                    else if(type.IsClass)
                    {
                        //collect all fields in the hierachy
                        var fields = new LinkedList<FieldInfo>();
                        foreach(var t in type.GetBaseTypes(includeTopLevel: true, includeInterfaces: false))
                            fields.AddAll(t.GetFields(BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic));
                        //constructs equality check
                        var expr = default(Expression);
                        foreach(var field in fields)
                        {
                            var fieldX = Expression.Field(x, field);
                            var fieldY = Expression.Field(y, field);
                            Expression condition;
                            if(field.FieldType.IsPointer || field.FieldType.IsPrimitive || field.FieldType.IsEnum)
                                condition = Expression.Equal(fieldX, fieldY);
                            else if(field.FieldType.IsValueType)
                                condition = Expression.Call(null, typeof(ValueType<>).MakeGenericType(field.FieldType).GetMethod(nameof(ValueType<int>.BitwiseEquals), new[] { field.FieldType, field.FieldType }), fieldX, fieldY);
                            else
                                condition = Expression.Call(null, typeof(object).GetMethod(nameof(Equals), new[] { typeof(object), typeof(object) }), fieldX, fieldY);    
                            expr = expr is null ? condition : Expression.AndAlso(expr, condition);
                        }
                        expr = expr is null ? Expression.ReferenceEqual(x, y) : Expression.OrElse(Expression.ReferenceEqual(x, y), expr);
                        comparer = Expression.Lambda<Func<T, T, bool>>(expr, false, x, y).Compile();
                        fields.Clear();
                    }
                    else    //interface
                    {
                        Instance = EqualityComparer<T>.Default;
                        return;
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