using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;

namespace Cheats.Reflection
{
    public readonly struct TypeSwitch<R>
    {
        public sealed class Builder
        {
            private readonly Dictionary<RuntimeTypeHandle, ValueHolder<R>> switchTable;

            internal Builder()
                => switchTable = new Dictionary<RuntimeTypeHandle, ValueHolder<R>>(RuntimeTypeHandleEqualityComparer.Instance);

            public Builder Add(Type type, Func<R> action)
            {
                switchTable.Add(type.TypeHandle, action);
                return this;
            }

            public Builder Add<T>(Func<R> action)
                => Add(typeof(T), action);
            
            public Builder Add(Type type, R value)
            {
                switchTable.Add(type.TypeHandle, value);
                return this;
            }

            public Builder Add<T>(R value)
                => Add(typeof(T), value);

            public TypeSwitch<R> Build()
                => new TypeSwitch<R>(switchTable);

            public static implicit operator TypeSwitch<R>(Builder builder)
                => builder is null ? default : builder.Build();
        }

        private readonly ReadOnlyDictionary<RuntimeTypeHandle, ValueHolder<R>> switchTable;

        private TypeSwitch(IDictionary<RuntimeTypeHandle, ValueHolder<R>> table)
            => switchTable = new ReadOnlyDictionary<RuntimeTypeHandle, ValueHolder<R>>(table);

        public Optional<R> Match(Type t)
        {
            if(switchTable is null || t is null)
                return default;
            else if(switchTable.TryGetValue(t.TypeHandle, out var action))
                return action.Value;
            else
                return default;
        }

        public static Builder Define() => new Builder();

        public Optional<R> Match<T>()
            => Match(typeof(T));
    }

    public readonly struct TypeSwitch<I, R>
    {
        public sealed class Builder
        {
            private readonly Dictionary<RuntimeTypeHandle, Func<I, R>> switchTable;
        
            internal Builder()
                => switchTable = new Dictionary<RuntimeTypeHandle, Func<I, R>>(RuntimeTypeHandleEqualityComparer.Instance);

            public Builder Add(Type type, Func<I, R> action)
            {
                switchTable.Add(type.TypeHandle, action);
                return this;
            }

            public Builder Add<T>(Func<I, R> action)
                => Add(typeof(T), action);

            public TypeSwitch<I, R> Build()
                => new TypeSwitch<I, R>(switchTable);

            public static implicit operator TypeSwitch<I, R>(Builder builder)
                => builder is null ? default : builder.Build();
        }

        private readonly ReadOnlyDictionary<RuntimeTypeHandle, Func<I, R>> switchTable;

        private TypeSwitch(IDictionary<RuntimeTypeHandle, Func<I, R>> table)
            => switchTable = new ReadOnlyDictionary<RuntimeTypeHandle, Func<I, R>>(table);

        public Optional<R> Match(I input, Type t)
        {
            if(switchTable is null || t is null)
                return default;
            else if(switchTable.TryGetValue(t.TypeHandle, out var action))
                return action(input);
            else
                return default;
        }

        public static Builder Define() => new Builder();

        public Optional<R> Match<T>(I input)
            => Match(input, typeof(T));
    }
}