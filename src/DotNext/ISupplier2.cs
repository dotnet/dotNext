using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DotNext
{
    public interface ISupplier<in T1, in T2, out TResult>
    {
        TResult Invoke(T1 arg1, T2 arg2);
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct Supplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>
    {
        private readonly delegate*<T1, T2, TResult> ptr;

        public Supplier(delegate*<T1, T2, TResult> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <inheritdoc />
        TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => ptr(arg1, arg2);

        public static implicit operator Supplier<T1, T2, TResult>(delegate*<T1, T2, TResult> ptr)
            => new Supplier<T1, T2, TResult>(ptr);
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct SupplierClosure<TContext, T1, T2, TResult> : ISupplier<T1, T2, TResult>
    {
        private readonly delegate*<in TContext, T1, T2, TResult> ptr;
        private readonly TContext context;

        public SupplierClosure(TContext context, delegate*<in TContext, T1, T2, TResult> ptr)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <inheritdoc />
        TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => ptr(in context, arg1, arg2);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingSupplier<T1, T2, TResult> : ISupplier<T1, T2, TResult>
    {
        private readonly Func<T1, T2, TResult> func;

        public DelegatingSupplier(Func<T1, T2, TResult> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        /// <inheritdoc />
        TResult ISupplier<T1, T2, TResult>.Invoke(T1 arg1, T2 arg2) => func(arg1, arg2);

        public static implicit operator DelegatingSupplier<T1, T2, TResult>(Func<T1, T2, TResult> func)
            => new DelegatingSupplier<T1, T2, TResult>(func);
    }

    internal readonly struct DelegatingComparer<T> : IComparer<T>, ISupplier<T?, T?, int>
    {
        private readonly Comparison<T?> comparison;

        public DelegatingComparer(Comparison<T?> comparison)
            => this.comparison = comparison ?? throw new ArgumentNullException(nameof(comparison));

        /// <inheritdoc />
        int ISupplier<T?, T?, int>.Invoke(T? x, T? y) => comparison(x, y);

        /// <inheritdoc />
        int IComparer<T>.Compare(T? x, T? y) => comparison(x, y);

        public static implicit operator DelegatingComparer<T>(Comparison<T?> comparison)
            => new DelegatingComparer<T>(comparison);
    }
}