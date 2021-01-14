using System;
using System.Runtime.InteropServices;

namespace DotNext
{
    public interface ISupplier<in T, out TResult>
    {
        TResult Invoke(T arg);
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct Supplier<T, TResult> : ISupplier<T, TResult>
    {
        private readonly delegate*<T, TResult> ptr;

        public Supplier(delegate*<T, TResult> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(arg);

        public static implicit operator Supplier<T, TResult>(delegate*<T, TResult> ptr)
            => new Supplier<T, TResult>(ptr);
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct SupplierClosure<TContext, T, TResult> : ISupplier<T, TResult>
    {
        private readonly delegate*<in TContext, T, TResult> ptr;
        private readonly TContext context;

        public SupplierClosure(delegate*<in TContext, T, TResult> ptr, TContext context)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => ptr(in context, arg);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingSupplier<T, TResult> : ISupplier<T, TResult>
    {
        private readonly Func<T, TResult> func;

        public DelegatingSupplier(Func<T, TResult> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        /// <inheritdoc />
        TResult ISupplier<T, TResult>.Invoke(T arg) => func(arg);

        public static implicit operator DelegatingSupplier<T, TResult>(Func<T, TResult> func)
            => new DelegatingSupplier<T, TResult>(func);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DelegatingPredicate<T> : ISupplier<T, bool>
    {
        private readonly Predicate<T> predicate;

        public DelegatingPredicate(Predicate<T> predicate)
            => this.predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));

        /// <inheritdoc />
        bool ISupplier<T, bool>.Invoke(T arg) => predicate(arg);

        public static implicit operator DelegatingPredicate<T>(Predicate<T> predicate)
            => new DelegatingPredicate<T>(predicate);
    }

    [StructLayout(LayoutKind.Auto)]
    internal readonly struct DelegatingConverter<TInput, TOutput> : ISupplier<TInput, TOutput>
    {
        private readonly Converter<TInput, TOutput> converter;

        public DelegatingConverter(Converter<TInput, TOutput> converter)
            => this.converter = converter ?? throw new ArgumentNullException(nameof(converter));

        /// <inheritdoc />
        TOutput ISupplier<TInput, TOutput>.Invoke(TInput arg) => converter(arg);

        public static implicit operator DelegatingConverter<TInput, TOutput>(Converter<TInput, TOutput> converter)
            => new DelegatingConverter<TInput, TOutput>(converter);
    }
}