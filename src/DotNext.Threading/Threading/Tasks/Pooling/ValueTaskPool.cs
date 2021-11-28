using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Threading.Tasks.Pooling;

[StructLayout(LayoutKind.Auto)]
internal struct ValueTaskPool<TNode>
    where TNode : ManualResetCompletionSource, IPooledManualResetCompletionSource<TNode>, new()
{
    private const int GrowFactor = 2;
    private const int MinimumGrow = 4;

    private readonly bool limited;
    private readonly Action<TNode> backToPool;
    private TNode?[] array;
    private int cursor;

    internal ValueTaskPool(Action<TNode> backToPool)
    {
        Debug.Assert(backToPool is not null);
        Debug.Assert((backToPool.Method.MethodImplementationFlags & MethodImplAttributes.Synchronized) != 0);

        limited = false;
        array = new TNode?[MinimumGrow];
        cursor = 0;
        this.backToPool = backToPool;
    }

    internal ValueTaskPool(Action<TNode> backToPool, int maximumRetained)
    {
        Debug.Assert(maximumRetained > 0);

        limited = true;
        array = new TNode?[maximumRetained];
        cursor = 0;
        this.backToPool = backToPool;
    }

    private void Grow()
    {
        if (array.Length == Array.MaxLength)
            throw new InsufficientMemoryException();

        var newLength = array.Length * GrowFactor;

        if ((uint)newLength > Array.MaxLength)
            newLength = Array.MaxLength;

        Array.Resize(ref array, Math.Max(newLength, MinimumGrow));
    }

    internal void Return(TNode node)
    {
        Debug.Assert(backToPool.Target is not null);
        Debug.Assert(Monitor.IsEntered(backToPool.Target));

        if (node.TryReset(out _))
        {
            node.OnConsumed = null;

            if ((uint)cursor >= (uint)array.Length)
            {
                if (limited)
                    return;

                Grow();
            }

            // avoid covariance check
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), cursor++) = node;
        }
    }

    internal TNode Get()
    {
        Debug.Assert(backToPool.Target is not null);
        Debug.Assert(Monitor.IsEntered(backToPool.Target));

        TNode result;
        if (cursor == 0)
        {
            result = new();
        }
        else
        {
            ref var holder = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), --cursor);
            Debug.Assert(holder is not null);

            result = holder;
            holder = null;
        }

        result.OnConsumed = backToPool;
        return result;
    }
}