using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading.Tasks;

public partial class TaskCompletionPipe<T>
{
    [StructLayout(LayoutKind.Auto)]
    private struct Queue
    {
        private const int GrowFactor = 2;
        private const int MinimumGrow = 10;

        private T[] array;
        private int head;       // The index from which to dequeue if the queue isn't empty.
        private int tail;       // The index at which to enqueue if the queue isn't full.

        internal Queue(int capacity)
        {
            array = capacity == 0 ? Array.Empty<T>() : new T[capacity];
            head = tail = 0;
        }

        internal readonly bool IsEmpty => head == tail;

        internal bool TryDequeue([MaybeNullWhen(false)]out T task)
        {
            if (IsEmpty)
            {
                task = default;
                return false;
            }

            ref var element = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), head++);
            task = element;
            element = null;
            return true;
        }

        private void Grow()
        {
            if (head > 0)
            {
                var newTail = tail - head;
                Array.Copy(array, head, array, 0, newTail);
                Array.Clear(array, newTail, head);
                head = 0;
                tail = newTail;
            }
            else if (array.Length < (uint)Array.MaxLength)
            {
                var newCapacity = GrowFactor * array.Length;
                if ((uint)newCapacity > (uint)Array.MaxLength)
                    newCapacity = Array.MaxLength;

                Array.Resize(ref array, Math.Max(newCapacity, array.Length + MinimumGrow));
            }
            else
            {
                throw new InsufficientMemoryException();
            }
        }

        internal void Enqueue(T task)
        {
            if ((uint)tail >= (uint)array.Length)
                Grow();

            // avoid covariance check
            Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(array), tail++) = task;
        }
    }

    private Queue completedTasks;
}