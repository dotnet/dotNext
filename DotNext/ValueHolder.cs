using System;

namespace DotNext
{
    internal readonly struct ValueHolder<T>
    {
        private readonly T value;
        private readonly Func<T> supplier;

        internal ValueHolder(T value)
        {
            this.value = value;
            supplier = null;
        }
        
        internal ValueHolder(Func<T> supplier)
        {
            value = default;
            this.supplier = supplier ?? throw new ArgumentNullException(nameof(supplier));
        }

        internal T Value => supplier is null ? value : supplier();

        public static implicit operator ValueHolder<T>(T value) => new ValueHolder<T>(value);

        public static implicit operator ValueHolder<T>(Func<T> supplier) => new ValueHolder<T>(supplier);

        public static implicit operator T(in ValueHolder<T> holder)
            => holder.Value;
    }
}