using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal struct ReferenceCounter(uint initialValue)
{
    private volatile uint value = initialValue;
    
    public bool TryAdd()
    {
        for (uint current = value, tmp; current > 0; current = tmp)
        {
            tmp = Interlocked.CompareExchange(ref value, current + 1, current);
            if (tmp == current)
                return true;
        }

        return false;
    }
    
    // the caller must own a reference
    public void Add()
    {
        Debug.Assert(value > 0U);

        Interlocked.Increment(ref value);
    }

    public bool Release()
    {
        Debug.Assert(value > 0U);

        return Interlocked.Decrement(ref value) is 0U;
    }

    [ExcludeFromCodeCoverage]
    public readonly override string ToString() => value.ToString();
}