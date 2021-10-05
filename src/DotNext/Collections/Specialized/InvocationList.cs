using System.Collections;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace DotNext.Collections.Specialized;

using Collections.Generic;

/// <summary>
/// Represents immutable list of delegates.
/// </summary>
/// <remarks>
/// This type can be used to store a list of event handlers in situations
/// when the delegate type allows variance. In this case, <see cref="Delegate.Combine(Delegate[])"/>
/// may not be applicable due to lack of variance support.
/// </remarks>
/// <typeparam name="TDelegate">The type of delegates in the list.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct InvocationList<TDelegate> : IReadOnlyCollection<TDelegate>
    where TDelegate : MulticastDelegate
{
    /// <summary>
    /// Represents enumerator over the list of delegates.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public struct Enumerator
    {
        private object? list;
        private int index;

        internal Enumerator(object? list)
        {
            this.list = list;
            index = -1;
            Current = default!;
        }

        /// <summary>
        /// Gets the current delegate.
        /// </summary>
        public TDelegate Current
        {
            get;
            private set;
        }

        /// <summary>
        /// Moves to the next delegate in the list.
        /// </summary>
        /// <returns><see langword="false"/> if the enumerator reaches the end of the list; otherwise, <see langword="true"/>.</returns>
        public bool MoveNext()
        {
            if (list is null)
                goto fail;

            if (list is TDelegate)
            {
                Current = Unsafe.As<TDelegate>(list);
                list = null;
                goto success;
            }

            var array = Unsafe.As<TDelegate[]>(list);
            index += 1;
            if ((uint)index >= (uint)array.Length)
                goto fail;

            Current = array[index];

        success:
            return true;
        fail:
            return false;
        }
    }

    // null, TDelegate or TDelegate[]
    private readonly object? list;

    private InvocationList(TDelegate d) => list = d;

    private InvocationList(TDelegate[] array, TDelegate d)
    {
        Array.Resize(ref array, array.Length + 1);
        array[array.Length - 1] = d;
        list = array;
    }

    private InvocationList(TDelegate d1, TDelegate d2)
        => list = new TDelegate[] { d1, d2 };

    private InvocationList(TDelegate[] array)
        => list = array;

    /// <summary>
    /// Indicates that this list is empty.
    /// </summary>
    public bool IsEmpty => list is null;

    /// <summary>
    /// Adds a delegate to the list and return a new list.
    /// </summary>
    /// <param name="d">The delegate to add.</param>
    /// <returns>The modified list of delegates.</returns>
    public InvocationList<TDelegate> Add(TDelegate? d)
    {
        InvocationList<TDelegate> result;
        if (d is null)
        {
            result = this;
        }
        else if (list is null)
        {
            result = new(d);
        }
        else if (list is TDelegate)
        {
            result = new(Unsafe.As<TDelegate>(list), d);
        }
        else
        {
            result = new(Unsafe.As<TDelegate[]>(list), d);
        }

        return result;
    }

    /// <summary>
    /// Removes the delegate from the list.
    /// </summary>
    /// <param name="d">The delegate to remove.</param>
    /// <returns>The modified list of delegates.</returns>
    public InvocationList<TDelegate> Remove(TDelegate? d)
    {
        InvocationList<TDelegate> result;

        if (d is null || list is null)
        {
            result = this;
        }
        else if (Equals(list, d))
        {
            result = default;
        }
        else
        {
            var array = Unsafe.As<TDelegate[]>(list);
            var index = Array.IndexOf(array, d);

            if (index >= 0)
                array = OneDimensionalArray.RemoveAt(array, index);

            result = new(array);
        }

        return result;
    }

    /// <summary>
    /// Gets the number of delegates in this list.
    /// </summary>
    public int Count
    {
        get
        {
            if (list is null)
                return 0;

            if (list is TDelegate)
                return 1;

            return Unsafe.As<TDelegate[]>(list).Length;
        }
    }

    /// <summary>
    /// Combines the delegates in the list to a single delegate.
    /// </summary>
    /// <returns>A list of delegates combined as a single delegate.</returns>
    public TDelegate? Combine()
    {
        if (list is null)
            return null;

        if (list is TDelegate)
            return Unsafe.As<TDelegate>(list);

        return Delegate.Combine(Unsafe.As<TDelegate[]>(list)) as TDelegate;
    }

    /// <summary>
    /// Addes the delegate to the list and returns modified list.
    /// </summary>
    /// <param name="list">The list of delegates.</param>
    /// <param name="d">The delegate to add.</param>
    /// <returns>The modified list of delegates.</returns>
    public static InvocationList<TDelegate> operator +(InvocationList<TDelegate> list, TDelegate? d)
        => list.Add(d);

    /// <summary>
    /// Removes the delegate from the list and returns modified list.
    /// </summary>
    /// <param name="list">The list of delegates.</param>
    /// <param name="d">The delegate to remove.</param>
    /// <returns>The modified list of delegates.</returns>
    public static InvocationList<TDelegate> operator -(InvocationList<TDelegate> list, TDelegate? d)
        => list.Remove(d);

    /// <summary>
    /// Gets enumerator over all delegates in this list.
    /// </summary>
    /// <returns>The enumerator over delegates.</returns>
    public Enumerator GetEnumerator() => new(list);

    private IEnumerator<TDelegate> GetEnumeratorCore()
    {
        if (list is null)
            return Sequence.GetEmptyEnumerator<TDelegate>();

        if (list is TDelegate d)
            return new SingletonList<TDelegate>.Enumerator(d);

        return Unsafe.As<TDelegate[]>(list).As<IEnumerable<TDelegate>>().GetEnumerator();
    }

    /// <inheritdoc />
    IEnumerator<TDelegate> IEnumerable<TDelegate>.GetEnumerator() => GetEnumeratorCore();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumeratorCore();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal ReadOnlySpan<TDelegate> Span
    {
        get
        {
            if (list is null)
                return ReadOnlySpan<TDelegate>.Empty;

            if (list is TDelegate)
                return MemoryMarshal.CreateReadOnlySpan(ref Unsafe.As<object, TDelegate>(ref Unsafe.AsRef(in list)), 1);

            return Unsafe.As<TDelegate[]>(list);
        }
    }
}

/// <summary>
/// Provides various extensions for <see cref="InvocationList{TDelegate}"/> type.
/// </summary>
public static class InvocationList
{
    /// <summary>
    /// Gets a span over the delegates in the list.
    /// </summary>
    /// <typeparam name="TDelegate">The type of the delegates.</typeparam>
    /// <param name="delegates">The list of delegates.</param>
    /// <returns>A span over the delegates in the list.</returns>
    public static ReadOnlySpan<TDelegate> AsSpan<TDelegate>(this ref InvocationList<TDelegate> delegates)
        where TDelegate : MulticastDelegate
        => delegates.Span;
}