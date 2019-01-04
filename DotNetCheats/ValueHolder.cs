using System;

namespace Cheats
{
    internal readonly struct ValueHolder<T>
    {
        private readonly T value;
        private readonly Func<T> supplier;

        public ValueHolder(T value)
        {
            this.value = value;
            this.supplier = null;
        }
        
        public ValueHolder(Func<T> supplier)
        {
            this.value = default;
            this.supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
        }

        public T Value => supplier is null ? value : supplier();

        public static implicit operator ValueHolder<T>(T value) => new ValueHolder<T>(value);

        public static implicit operator ValueHolder<T>(Func<T> supplier) => new ValueHolder<T>(supplier);

        public static implicit operator T(in ValueHolder<T> holder)
            => holder.Value;
    }
}