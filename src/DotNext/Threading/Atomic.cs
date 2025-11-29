using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;

/// <summary>
/// Provides atomic access to non-primitive data type.
/// </summary>
/// <typeparam name="T">The type of the value to be accessible in atomic manner.</typeparam>
/// <remarks>
/// Synchronized methods can be declared in classes only. If you don't need to have extra heap allocation
/// to keep synchronization root in the form of the object, or you need to have volatile field
/// inside of value type then <see cref="Atomic{T}"/> is the best choice. Its performance is better
/// than synchronized methods according to benchmarks.
/// </remarks>
[StructLayout(LayoutKind.Auto)]
public struct Atomic<T> : IStrongBox, ICloneable
    where T : struct
{
    private interface IEqualityComparer
    {
        bool Equals(in T x, in T y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct BitwiseEqualityComparer : IEqualityComparer
    {
        bool IEqualityComparer.Equals(in T x, in T y) => EqualityComparer<T>.Default.Equals(x, y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingEqualityComparer : IEqualityComparer
    {
        private readonly Func<T, T, bool> func;

        internal DelegatingEqualityComparer(Func<T, T, bool> func)
            => this.func = func ?? throw new ArgumentNullException(nameof(func));

        bool IEqualityComparer.Equals(in T x, in T y) => func(x, y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly unsafe struct EqualityComparer : IEqualityComparer
    {
        private readonly delegate*<in T, in T, bool> ptr;

        internal EqualityComparer(delegate*<in T, in T, bool> ptr)
            => this.ptr = ptr is not null ? ptr : throw new ArgumentNullException(nameof(ptr));

        bool IEqualityComparer.Equals(in T x, in T y) => ptr(in x, in y);
    }

    /// <summary>
    /// Represents atomic update action.
    /// </summary>
    /// <remarks>The atomic update action should side-effect free.</remarks>
    /// <param name="current">The value to update.</param>
    public delegate void Updater(ref T current);

    /// <summary>
    /// Represents atomic accumulator.
    /// </summary>
    /// <remarks>The atomic accumulator should side-effect free.</remarks>
    /// <param name="current">The value to update.</param>
    /// <param name="x">The value to be combined with <paramref name="current"/>.</param>
    public delegate void Accumulator(ref T current, in T x);

    private T value;
    private bool lockState;

    /// <summary>
    /// Clones this container atomically.
    /// </summary>
    /// <returns>The cloned container.</returns>
    public Atomic<T> Clone()
    {
        var result = new Atomic<T>();
        Read(out result.value);
        return result;
    }

    /// <inheritdoc/>
    object ICloneable.Clone() => Clone();

    /// <summary>
    /// Performs atomic read.
    /// </summary>
    /// <param name="result">The result of atomic read.</param>
    public void Read(out T result)
    {
        Interlocked.Acquire(ref lockState);
        AdvancedHelpers.Copy(in value, out result);
        Interlocked.Release(ref lockState);
    }

    /// <summary>
    /// Swaps the value stored in this container and the given value atomically.
    /// </summary>
    /// <remarks>
    /// This operation is atomic for both containers.
    /// </remarks>
    /// <param name="other">The container for the value.</param>
    public void Swap(ref Atomic<T> other)
    {
        Interlocked.Acquire(ref lockState);
        other.Swap(ref value);
        Interlocked.Release(ref lockState);
    }

    /// <summary>
    /// Swaps the value stored in this container and the given value atomically.
    /// </summary>
    /// <param name="other">The managed pointer to the value to swap.</param>
    public void Swap(ref T other)
    {
        Interlocked.Acquire(ref lockState);
        AdvancedHelpers.Swap(ref value, ref other);
        Interlocked.Release(ref lockState);
    }

    /// <summary>
    /// Performs atomic write.
    /// </summary>
    /// <param name="newValue">The value to be stored into this container.</param>
    public void Write(in T newValue)
    {
        Interlocked.Acquire(ref lockState);
        AdvancedHelpers.Copy(in newValue, out value);
        Interlocked.Release(ref lockState);
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool CompareExchange<TComparer>(TComparer comparer, in T update, in T expected, out T result)
        where TComparer : struct, IEqualityComparer
    {
        bool successful;
        Interlocked.Acquire(ref lockState);
        var current = value;
        if (successful = comparer.Equals(in current, in expected))
            AdvancedHelpers.Copy(in update, out value);
        AdvancedHelpers.Copy(in current, out result);
        Interlocked.Release(ref lockState);
        return successful;
    }

    /// <summary>
    /// Compares two values of type <typeparamref name="T"/> for bitwise equality and, if they are equal, replaces the stored value.
    /// </summary>
    /// <param name="update">The value that replaces the stored value if the comparison results in equality.</param>
    /// <param name="expected">The value that is compared to the stored value.</param>
    /// <param name="result">The origin value stored in this container before modification.</param>
    /// <returns><see langword="true"/> if the current value is replaced by <paramref name="update"/>; otherwise, <see langword="false"/>.</returns>
    public bool CompareExchange(in T update, in T expected, out T result)
        => CompareExchange(new BitwiseEqualityComparer(), in update, in expected, out result);

    /// <summary>
    /// Compares two values of type <typeparamref name="T"/> for equality and, if they are equal, replaces the stored value.
    /// </summary>
    /// <param name="comparer">The function representing comparison logic.</param>
    /// <param name="update">The value that replaces the stored value if the comparison results in equality.</param>
    /// <param name="expected">The value that is compared to the stored value.</param>
    /// <param name="result">The origin value stored in this container before modification.</param>
    /// <returns><see langword="true"/> if the current value is replaced by <paramref name="update"/>; otherwise, <see langword="false"/>.</returns>
    public bool CompareExchange(Func<T, T, bool> comparer, in T update, in T expected, out T result)
        => CompareExchange(new DelegatingEqualityComparer(comparer), in update, in expected, out result);

    /// <summary>
    /// Compares two values of type <typeparamref name="T"/> for equality and, if they are equal, replaces the stored value.
    /// </summary>
    /// <param name="comparer">The function representing comparison logic.</param>
    /// <param name="update">The value that replaces the stored value if the comparison results in equality.</param>
    /// <param name="expected">The value that is compared to the stored value.</param>
    /// <param name="result">The origin value stored in this container before modification.</param>
    /// <returns><see langword="true"/> if the current value is replaced by <paramref name="update"/>; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public unsafe bool CompareExchange(delegate*<in T, in T, bool> comparer, in T update, in T expected, out T result)
        => CompareExchange(new EqualityComparer(comparer), in update, in expected, out result);

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool CompareAndSet<TComparer>(TComparer comparer, in T expected, in T update)
        where TComparer : struct, IEqualityComparer
    {
        bool result;
        Interlocked.Acquire(ref lockState);
        try
        {
            // custom comparer may throw exception
            if (result = comparer.Equals(in value, in expected))
                AdvancedHelpers.Copy(in update, out value);
        }
        finally
        {
            Interlocked.Release(ref lockState);
        }

        return result;
    }

    /// <summary>
    /// Atomically sets the stored value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    public bool CompareAndSet(in T expected, in T update)
        => CompareAndSet(new BitwiseEqualityComparer(), in expected, in update);

    /// <summary>
    /// Atomically sets the stored value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="comparer">The function representing comparison logic.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    public bool CompareAndSet(Func<T, T, bool> comparer, in T expected, in T update)
        => CompareAndSet(new DelegatingEqualityComparer(comparer), in expected, in update);

    /// <summary>
    /// Atomically sets the stored value to the given updated value if the current value == the expected value.
    /// </summary>
    /// <param name="comparer">The function representing comparison logic.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="update">The new value.</param>
    /// <returns><see langword="true"/> if successful. <see langword="false"/> return indicates that the actual value was not equal to the expected value.</returns>
    [CLSCompliant(false)]
    public unsafe bool CompareAndSet(delegate*<in T, in T, bool> comparer, in T expected, in T update)
        => CompareAndSet(new EqualityComparer(comparer), in expected, in update);

    /// <summary>
    /// Sets a value stored in this container to a specified value and returns the original value, as an atomic operation.
    /// </summary>
    /// <param name="update">The value that replaces the stored value.</param>
    /// <param name="previous">The original stored value before modification.</param>
    public void Exchange(in T update, out T previous)
    {
        Interlocked.Acquire(ref lockState);
        AdvancedHelpers.Copy(in value, out previous);
        AdvancedHelpers.Copy(in update, out value);
        Interlocked.Release(ref lockState);
    }

    /// <summary>
    /// Atomically updates the stored value with the results of applying the given function, returning the updated value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <param name="result">The updated value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void UpdateAndGet(Updater updater, out T result)
    {
        ArgumentNullException.ThrowIfNull(updater);

        Interlocked.Acquire(ref lockState);
        try
        {
            // custom updater may throw exception
            updater(ref value);
            AdvancedHelpers.Copy(in value, out result);
        }
        finally
        {
            Interlocked.Release(ref lockState);
        }
    }

    /// <summary>
    /// Atomically updates the stored value with the results
    /// of applying the given function, returning the original value.
    /// </summary>
    /// <param name="updater">A side-effect-free function.</param>
    /// <param name="result">The original value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="updater"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void GetAndUpdate(Updater updater, out T result)
    {
        ArgumentNullException.ThrowIfNull(updater);

        Interlocked.Acquire(ref lockState);
        var previous = value;
        try
        {
            // custom updater may throw exception
            updater(ref value);
        }
        finally
        {
            Interlocked.Release(ref lockState);
        }

        AdvancedHelpers.Copy(in previous, out result);
    }

    /// <summary>
    /// Atomically updates the stored value with the results of applying the given function
    /// to the current and given values, returning the original value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <param name="result">The updated value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="accumulator"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void AccumulateAndGet(in T x, Accumulator accumulator, out T result)
    {
        ArgumentNullException.ThrowIfNull(accumulator);

        Interlocked.Acquire(ref lockState);
        try
        {
            // custom accumulator may throw exception
            accumulator(ref value, in x);
            AdvancedHelpers.Copy(in value, out result);
        }
        finally
        {
            Interlocked.Release(ref lockState);
        }
    }

    /// <summary>
    /// Atomically updates the stored value with the results of applying the given function
    /// to the current and given values, returning the updated value.
    /// </summary>
    /// <remarks>
    /// The function is applied with the current value as its first argument, and the given update as the second argument.
    /// </remarks>
    /// <param name="x">Accumulator operand.</param>
    /// <param name="accumulator">A side-effect-free function of two arguments.</param>
    /// <param name="result">The original value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="accumulator"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void GetAndAccumulate(in T x, Accumulator accumulator, out T result)
    {
        ArgumentNullException.ThrowIfNull(accumulator);

        Interlocked.Acquire(ref lockState);
        var previous = value;
        try
        {
            // custom accumulator may throw exception
            accumulator(ref value, in x);
        }
        finally
        {
            Interlocked.Release(ref lockState);
        }

        AdvancedHelpers.Copy(in previous, out result);
    }

    /// <summary>
    /// Gets or sets value atomically.
    /// </summary>
    /// <remarks>
    /// To achieve the best performance it is recommended to use <see cref="Read"/> and <see cref="Write"/> methods
    /// because they don't cause extra allocation of stack memory for passing value.
    /// </remarks>
    public T Value
    {
        get
        {
            Read(out var result);
            return result;
        }
        set => Write(value);
    }

    /// <inheritdoc/>
    object? IStrongBox.Value
    {
        get => Value;
        set => Value = (T)value!;
    }

    /// <summary>
    /// Converts the stored value into string atomically.
    /// </summary>
    /// <returns>The string returned from <see cref="object.ToString"/> method called on the stored value.</returns>
    public override string? ToString()
    {
        Read(out var result);
        return result.ToString();
    }
}

/// <summary>
/// Exposes atomic operations for thread-safe scenarios.
/// </summary>
public static partial class Atomic
{
    private static bool Is32BitProcess => nuint.Size < sizeof(ulong);
    
    private static (TValue OldValue, TValue NewValue) Update<TValue, TUpdater>(ref TValue value, TUpdater updater)
        where TUpdater : ISupplier<TValue, TValue>, allows ref struct
    {
        TValue oldValue, newValue, tmp = value;
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        } while (!EqualityComparer<TValue>
                     .Default
                     .Equals(oldValue, tmp = Interlocked.CompareExchange(ref value!, newValue, oldValue)));

        return (oldValue, newValue);
    }

    private static (TValue OldValue, TValue NewValue) Accumulate<TValue, TAccumulator>(ref TValue value, TValue x, TAccumulator accumulator)
        where TAccumulator : ISupplier<TValue, TValue, TValue>, allows ref struct
    {
        TValue oldValue, newValue, tmp = value;
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        } while (!EqualityComparer<TValue>
                     .Default
                     .Equals(oldValue, tmp = Interlocked.CompareExchange(ref value!, newValue, oldValue)));

        return (oldValue, newValue);
    }

    /// <summary>
    /// Introduces additional methods to <see cref="Interlocked"/> type.
    /// </summary>
    /// <typeparam name="T">
    /// The type to be used for memory location,and comparand.
    /// This type must be a reference type, an enum type (i.e. <c>typeof(T).IsEnum</c> is <see langword="true"/>),
    /// or a primitive type (i.e. <c>typeof(T).IsPrimitive</c> is <see langword="true"/>).
    /// </typeparam>
    extension<T>(Interlocked)
    {
        /// <summary>
        /// Determines that write to the location in the memory of
        /// type <typeparamref name="T"/> is atomic.
        /// </summary>
        /// <typeparam name="T">The type of the value to be written.</typeparam>
        /// <returns><see langword="true"/> if write is atomic; otherwise, <see langword="false"/>.</returns>
        /// <seelaso href="https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf">Section I.12.6.6.</seelaso>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAtomic()
            => AdvancedHelpers.AlignOf<T>() == Unsafe.SizeOf<T>() && Unsafe.SizeOf<T>() <= nuint.Size;
        
        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T AccumulateAndGet<TAccumulator>(ref T location, T x, TAccumulator accumulator)
            where TAccumulator : ISupplier<T, T, T>, allows ref struct
            => Accumulate(ref location, x, accumulator).NewValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the updated value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T AccumulateAndGet(ref T location, T x, Func<T, T, T> accumulator)
            => AccumulateAndGet<T, DelegatingSupplier<T, T, T>>(ref location, x, accumulator);

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <typeparam name="TAccumulator">The type implementing accumulator.</typeparam>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T GetAndAccumulate<TAccumulator>(ref T location, T x, TAccumulator accumulator)
            where TAccumulator : ISupplier<T, T, T>, allows ref struct
            => Accumulate(ref location, x, accumulator).OldValue;

        /// <summary>
        /// Atomically updates the current value with the results of applying the given function
        /// to the current and given values, returning the original value.
        /// </summary>
        /// <remarks>
        /// The function is applied with the current value as its first argument, and the given update as the second argument.
        /// </remarks>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="x">Accumulator operand.</param>
        /// <param name="accumulator">A side-effect-free function of two arguments.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T GetAndAccumulate(ref T location, T x, Func<T, T, T> accumulator)
            => GetAndAccumulate<T, DelegatingSupplier<T, T, T>>(ref location, x, accumulator);

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T UpdateAndGet<TUpdater>(ref T location, TUpdater updater)
            where TUpdater : ISupplier<T, T>, allows ref struct
            => Update(ref location, updater).NewValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the updated value.
        /// </summary>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The updated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T UpdateAndGet(ref T location, Func<T, T> updater)
            => UpdateAndGet<T, DelegatingSupplier<T, T>>(ref location, updater);

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <typeparam name="TUpdater">The type implementing updater.</typeparam>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T GetAndUpdate<TUpdater>(ref T location, TUpdater updater)
            where TUpdater : ISupplier<T, T>, allows ref struct
            => Update(ref location, updater).OldValue;

        /// <summary>
        /// Atomically updates the stored value with the results
        /// of applying the given function, returning the original value.
        /// </summary>
        /// <param name="location">Reference to a value to be modified.</param>
        /// <param name="updater">A side-effect-free function.</param>
        /// <returns>The original value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [return: NotNullIfNotNull(nameof(location))]
        public static T GetAndUpdate(ref T location, Func<T, T> updater)
            => GetAndUpdate<T, DelegatingSupplier<T, T>>(ref location, updater);
    }
}