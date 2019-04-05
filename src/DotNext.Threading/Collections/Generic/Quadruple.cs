using System.Collections;
using System.Collections.Generic;

namespace DotNext.Collections.Generic
{
    internal struct Quadruple<T>: IEnumerator<T>
    {
        internal const short Count = 4;
        internal T First, Second, Third, Fourth;
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
                    case 2:
                        return Second;
                    case 3:
                        return Third;
                    default:
                        return Fourth;
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