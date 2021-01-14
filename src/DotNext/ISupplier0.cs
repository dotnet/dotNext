using System;
using System.Runtime.InteropServices;

namespace DotNext
{
    /// <summary>
    /// Represents functional interface returning arbitrary value.
    /// </summary>
    /// <remarks>
    /// Functional interface can be used as a way to pass
    /// captured variables without heap allocation.
    /// </remarks>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    public interface ISupplier<out TResult>
    {
        /// <summary>
        /// Invokes supplier.
        /// </summary>
        /// <returns>The result value returned by this supplier.</returns>
        TResult Invoke();
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct Supplier<TResult> : ISupplier<TResult>
    {
        private readonly delegate*<TResult> ptr;

        public Supplier(delegate*<TResult> ptr)
            => this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => ptr();

        public static Supplier<TResult> Activator => new Supplier<TResult>(&System.Activator.CreateInstance<TResult>);

        public static implicit operator Supplier<TResult>(delegate*<TResult> ptr)
            => new Supplier<TResult>(ptr);
    }

    [StructLayout(LayoutKind.Auto)]
    [CLSCompliant(false)]
    public readonly unsafe struct SupplierClosure<TContext, TResult> : ISupplier<TResult>
    {
        private readonly delegate*<in TContext, TResult> ptr;
        private readonly TContext context;

        public SupplierClosure(delegate*<in TContext, TResult> ptr, TContext context)
        {
            this.ptr = ptr == null ? throw new ArgumentNullException(nameof(ptr)) : ptr;
            this.context = context;
        }

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => ptr(in context);
    }

    [StructLayout(LayoutKind.Auto)]
    public readonly struct DelegatingSupplier<TResult> : ISupplier<TResult>
    {
        private readonly Func<TResult> func;

        public DelegatingSupplier(Func<TResult> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        /// <inheritdoc />
        TResult ISupplier<TResult>.Invoke() => func();

        public static implicit operator DelegatingSupplier<TResult>(Func<TResult> func) => new (func);
    }
}