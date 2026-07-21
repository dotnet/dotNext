using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Collections.Concurrent;

[StructLayout(LayoutKind.Auto)]
internal struct ConcurrentGrowableArray<T>()
{
    private readonly Lock syncRoot = new();
    private T[] array = [];

    public T[] Array
    {
        readonly get => Volatile.Read(in array);
        set => Volatile.Write(ref array, value);
    }

    public readonly ref T TryGet(int index)
    {
        Debug.Assert(index >= 0);

        var arrayCopy = Array;
        return ref (uint)index < (uint)arrayCopy.Length
            ? ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index)
            : ref Unsafe.NullRef<T>();
    }

    public ref T Get<TInitializer>(int index)
        where TInitializer : IElementInitializer, allows ref struct
    {
        Debug.Assert(index >= 0);

        var arrayCopy = Array;
        if ((uint)index >= (uint)arrayCopy.Length)
            arrayCopy = Resize<TInitializer>(index);

        return ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arrayCopy), (uint)index);
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
                TInitializer.Initialize(out item);
            }
        }
    }

    public interface IElementInitializer
    {
        public static abstract void Initialize(out T value);
    }
}

internal static class ConcurrentGrowableArray
{
    public static ref T Get<T>(this ref ConcurrentGrowableArray<T> array, int index)
        where T : ConcurrentGrowableArray<T>.IElementInitializer
        => ref array.Get<T>(index);
}