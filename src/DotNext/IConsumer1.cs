using System;
using System.Runtime.InteropServices;

namespace DotNext
{
    public interface IConsumer<in T>
    {
        void Invoke(T value);
    }

    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct Consumer<T> : IConsumer<T>
    {
        private readonly delegate*<T, void> ptr;

        public Consumer(delegate*<T, void> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <inheritdoc />
        void IConsumer<T>.Invoke(T arg) => ptr(arg);

        public static implicit operator Consumer<T>(delegate*<T, void> ptr)
            => new Consumer<T>(ptr);
    }

    [CLSCompliant(false)]
    [StructLayout(LayoutKind.Auto)]
    public readonly unsafe struct ConsumerClosure<TContext, T> : IConsumer<T>
    {
        private readonly delegate*<in TContext, T, void> ptr;
        private readonly TContext context;

        public ConsumerClosure(delegate*<in TContext, T, void> ptr, TContext context)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <inheritdoc />
        void IConsumer<T>.Invoke(T arg) => ptr(in context, arg);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingConsumer<T> : IConsumer<T>
    {
        private readonly Action<T> action;

        public DelegatingConsumer(Action<T> action)
            => this.action = action ?? throw new ArgumentNullException(nameof(action));

        /// <inheritdoc />
        void IConsumer<T>.Invoke(T arg) => action(arg);
    }
}