﻿using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using static Runtime.Intrinsics;

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

    private Atomic.Boolean lockState;

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
        lockState.Acquire();
        Copy(in value, out result);
        lockState.Release();
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
        lockState.Acquire();
        other.Swap(ref value);
        lockState.Release();
    }

    /// <summary>
    /// Swaps the value stored in this container and the given value atomically.
    /// </summary>
    /// <param name="other">The managed pointer to the value to swap.</param>
    public void Swap(ref T other)
    {
        lockState.Acquire();
        Runtime.Intrinsics.Swap(ref value, ref other);
        lockState.Release();
    }

    /// <summary>
    /// Performs atomic write.
    /// </summary>
    /// <param name="newValue">The value to be stored into this container.</param>
    public void Write(in T newValue)
    {
        lockState.Acquire();
        Copy(in newValue, out value);
        lockState.Release();
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private bool CompareExchange<TComparer>(TComparer comparer, in T update, in T expected, out T result)
        where TComparer : struct, IEqualityComparer
    {
        bool successful;
        lockState.Acquire();
        var current = value;
        if (successful = comparer.Equals(in current, in expected))
            Copy(in update, out value);
        Copy(in current, out result);
        lockState.Release();
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
        lockState.Acquire();
        try
        {
            // custom comparer may throw exception
            if (result = comparer.Equals(in value, in expected))
                Copy(in update, out value);
        }
        finally
        {
            lockState.Release();
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
        lockState.Acquire();
        Copy(in value, out previous);
        Copy(in update, out value);
        lockState.Release();
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

        lockState.Acquire();
        try
        {
            // custom updater may throw exception
            updater(ref value);
            Copy(in value, out result);
        }
        finally
        {
            lockState.Release();
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

        lockState.Acquire();
        var previous = value;
        try
        {
            // custom updater may throw exception
            updater(ref value);
        }
        finally
        {
            lockState.Release();
        }

        Copy(in previous, out result);
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

        lockState.Acquire();
        try
        {
            // custom accumulator may throw exception
            accumulator(ref value, in x);
            Copy(in value, out result);
        }
        finally
        {
            lockState.Release();
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

        lockState.Acquire();
        var previous = value;
        try
        {
            // custom accumulator may throw exception
            accumulator(ref value, in x);
        }
        finally
        {
            lockState.Release();
        }

        Copy(in previous, out result);
    }

    /// <summary>
    /// Gets or sets value atomically.
    /// </summary>
    /// <remarks>
    /// To achieve best performance it is recommended to use <see cref="Read"/> and <see cref="Write"/> methods
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
    private static (TValue OldValue, TValue NewValue) Update<TValue, TUpdater, TOperations>(ref TValue value, TUpdater updater)
        where TValue : IEqualityOperators<TValue, TValue, bool>
        where TUpdater : notnull, ISupplier<TValue, TValue>
        where TOperations : IInterlockedOperations<TValue>
    {
        TValue oldValue, newValue, tmp = TOperations.VolatileRead(in value);
        do
        {
            newValue = updater.Invoke(oldValue = tmp);
        }
        while ((tmp = TOperations.CompareExchange(ref value, newValue, oldValue)) != oldValue);

        return (oldValue, newValue);
    }

    private static (TValue OldValue, TValue NewValue) Accumulate<TValue, TAccumulator, TOperations>(ref TValue value, TValue x, TAccumulator accumulator)
        where TValue : IEqualityOperators<TValue, TValue, bool>
        where TAccumulator : notnull, ISupplier<TValue, TValue, TValue>
        where TOperations : IInterlockedOperations<TValue>
    {
        TValue oldValue, newValue, tmp = TOperations.VolatileRead(in value);
        do
        {
            newValue = accumulator.Invoke(oldValue = tmp, x);
        }
        while ((tmp = TOperations.CompareExchange(ref value, newValue, oldValue)) != oldValue);

        return (oldValue, newValue);
    }

    /// <summary>
    /// Determines that write to the location in the memory of
    /// type <typeparamref name="T"/> is atomic.
    /// </summary>
    /// <typeparam name="T">The type of the value to be written.</typeparam>
    /// <returns><see langword="true"/> if write is atomic; otherwise, <see langword="false"/>.</returns>
    /// <seelaso href="https://www.ecma-international.org/publications/files/ECMA-ST/ECMA-335.pdf">Section I.12.6.6.</seelaso>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAtomic<T>()
        => AlignOf<T>() == Unsafe.SizeOf<T>() && Unsafe.SizeOf<T>() <= nuint.Size;

    private struct InterlockedOperations :
        IInterlockedOperations<int>,
        IInterlockedOperations<uint>,
        IInterlockedOperations<long>,
        IInterlockedOperations<ulong>,
        IInterlockedOperations<nint>,
        IInterlockedOperations<nuint>,
        IInterlockedOperations<float>,
        IInterlockedOperations<double>
    {
        static int IInterlockedOperations<int>.VolatileRead(ref readonly int location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<int>.VolatileWrite(ref int location, int value)
            => Volatile.Write(ref location, value);

        static int IInterlockedOperations<int>.CompareExchange(ref int location, int value, int comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static uint IInterlockedOperations<uint>.VolatileRead(ref readonly uint location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<uint>.VolatileWrite(ref uint location, uint value)
            => Volatile.Write(ref location, value);

        static uint IInterlockedOperations<uint>.CompareExchange(ref uint location, uint value, uint comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static long IInterlockedOperations<long>.VolatileRead(ref readonly long location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<long>.VolatileWrite(ref long location, long value)
            => Volatile.Write(ref location, value);

        static long IInterlockedOperations<long>.CompareExchange(ref long location, long value, long comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static ulong IInterlockedOperations<ulong>.VolatileRead(ref readonly ulong location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<ulong>.VolatileWrite(ref ulong location, ulong value)
            => Volatile.Write(ref location, value);

        static ulong IInterlockedOperations<ulong>.CompareExchange(ref ulong location, ulong value, ulong comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static nint IInterlockedOperations<nint>.VolatileRead(ref readonly nint location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<nint>.VolatileWrite(ref nint location, nint value)
            => Volatile.Write(ref location, value);

        static nint IInterlockedOperations<nint>.CompareExchange(ref nint location, nint value, nint comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static nuint IInterlockedOperations<nuint>.VolatileRead(ref readonly nuint location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<nuint>.VolatileWrite(ref nuint location, nuint value)
            => Volatile.Write(ref location, value);

        static nuint IInterlockedOperations<nuint>.CompareExchange(ref nuint location, nuint value, nuint comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static float IInterlockedOperations<float>.VolatileRead(ref readonly float location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<float>.VolatileWrite(ref float location, float value)
            => Volatile.Write(ref location, value);

        static float IInterlockedOperations<float>.CompareExchange(ref float location, float value, float comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);

        static double IInterlockedOperations<double>.VolatileRead(ref readonly double location)
            => Volatile.Read(in location);

        static void IInterlockedOperations<double>.VolatileWrite(ref double location, double value)
            => Volatile.Write(ref location, value);

        static double IInterlockedOperations<double>.CompareExchange(ref double location, double value, double comparand)
            => Interlocked.CompareExchange(ref location, value, comparand);
    }
}