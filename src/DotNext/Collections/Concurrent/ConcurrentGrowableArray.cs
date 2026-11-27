using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

[StructLayout(LayoutKind.Auto)]
internal struct ConcurrentGrowableArray<T>()
    where T : class?
{
    private readonly Lock syncRoot = new();
    private T[] array = [];

    public T[] Array
    {
        readonly get => Volatile.Read(in array);
        set
        {
            // synchronized with Resize, otherwise the resized old array can overwrite the value
            lock (syncRoot)
            {
                array = value;
            }
        }
    }

    public readonly bool TryGet(int index, [MaybeNullWhen(false)] out T result)
    {
        Debug.Assert(index >= 0);

        var arrayCopy = Array;
        if ((uint)index < (uint)arrayCopy.Length)
        {
            result = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index);
            return true;
        }

        result = null;
        return false;
    }

    public T Get<TInitializer>(int index)
        where TInitializer : IElementInitializer, allows ref struct
    {
        Debug.Assert(index >= 0);

        var arrayCopy = Array;
        if ((uint)index >= (uint)arrayCopy.Length)
            arrayCopy = Resize<TInitializer>(index);

        return Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index);
    }

    private T[] Resize<TInitializer>(int index)
        where TInitializer : IElementInitializer, allows ref struct
    {
        T[] arrayCopy;
        lock (syncRoot)
        {
            arrayCopy = array;
            var length = arrayCopy.Length;

            if ((uint)index >= (uint)length)
            {
                System.Array.Resize(ref arrayCopy, index + 1);
                Initialize(arrayCopy.AsSpan(length));
                array = arrayCopy;
            }
        }

        return arrayCopy;

        static void Initialize(Span<T> array)
        {
            foreach (ref var item in array)
            {
                item = TInitializer.Create();
            }
        }
    }

    // the element is initialized lazily, so the array may contain null elements
    [return: NotNull]
    public T GetOrAdd<TInitializer>(int index)
        where TInitializer : IElementInitializer, allows ref struct
    {
        Debug.Assert(index >= 0);

        var arrayCopy = Array;
        return (uint)index < (uint)arrayCopy.Length
               && Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index) is { } item
            ? item
            : Add<TInitializer>(index);
    }

    [return: NotNull]
    private T Add<TInitializer>(int index)
        where TInitializer : IElementInitializer, allows ref struct
    {
        lock (syncRoot)
        {
            // the element must be written to the current array, otherwise it can be lost by Resize
            var arrayCopy = array;
            if ((uint)index >= (uint)arrayCopy.Length)
            {
                System.Array.Resize(ref arrayCopy, index + 1);
                array = arrayCopy;
            }

            ref var item = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index);
            return item ??= TInitializer.Create();
        }
    }

    public interface IElementInitializer
    {
        [return: NotNull]
        public static abstract T Create();
    }
}

internal static class ConcurrentGrowableArray
{
    // TODO: Migrate to extension indexer
    public static T Get<T>(this ref ConcurrentGrowableArray<T> array, int index)
        where T : class?, new()
        => array.Get<DefaultInitializer<T>>(index);

    [return: NotNull]
    public static T GetOrAdd<T>(this ref ConcurrentGrowableArray<T> array, int index)
        where T : class?, new()
        => array.GetOrAdd<DefaultInitializer<T>>(index);
}

[StructLayout(LayoutKind.Auto)]
file readonly ref struct DefaultInitializer<T> : ConcurrentGrowableArray<T>.IElementInitializer
    where T : class?, new()
{
    [return: NotNull]
    static T ConcurrentGrowableArray<T>.IElementInitializer.Create()
        => new();
}