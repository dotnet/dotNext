using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    internal struct Pair<T>: IEnumerator<T>
    {
        internal const short Count = 2;
        internal T First, Second;
        private short index;

        private T Current
        {
            get
            {
                switch(index)
                {
                    case 0:
                    case 1:
                        return First;
                    default:
                        return Second;
                }
            }
        }

        T IEnumerator<T>.Current => Current;

        object IEnumerator.Current => Current;

        bool IEnumerator.MoveNext()
        {
            index += 1;
            return index <= Count;
        }

        void IEnumerator.Reset() => index = 0;

        void IDisposable.Dispose() => this = default;
    }
}